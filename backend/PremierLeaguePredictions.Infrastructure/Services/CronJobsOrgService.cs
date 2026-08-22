using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;

namespace PremierLeaguePredictions.Infrastructure.Services;

public class CronJobsOrgService : ICronJobsOrgService
{
    private readonly CronJobsOrgClient _client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CronJobsOrgService> _logger;

    // Jobs created by this app are prefixed so they can be found and cleaned up.
    // The prefix includes the environment (e.g. "EPL-DEV-", "EPL-PROD-") so that when
    // dev and prod share a cron-job.org account, each environment's generate only
    // deletes/replaces its own jobs and never interferes with the other's.
    private const string JobTitlePrefixBase = "EPL-";

    private static string BuildJobPrefix(string environment) =>
        $"{JobTitlePrefixBase}{environment.ToUpperInvariant()}-";

    // sync-scores stays a direct cron-job.org -> API call (protects the GitHub Actions
    // minutes budget: every-2-min syncs would burn thousands of minutes). It self-warms
    // once running, so we only need one wake shortly before each window.
    private const string SyncEndpoint = "/api/v1/admin/sync/results";

    // How far ahead of a sync window to fire the wake, allowing for GitHub queue +
    // runner spin-up + the ~20s Render cold start before the first sync call lands.
    private static readonly TimeSpan SyncWakeLeadTime = TimeSpan.FromMinutes(5);

    // Job types that run through GitHub Actions (wake + call), mapped to the
    // orchestrator workflow's "action" value.
    private static readonly Dictionary<string, string> DispatchActions = new()
    {
        ["send-reminders"] = "reminders",
        ["auto-pick"]      = "auto-pick",
    };

    public CronJobsOrgService(
        CronJobsOrgClient client,
        IConfiguration configuration,
        ILogger<CronJobsOrgService> logger)
    {
        _client = client;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ScheduleGenerationResponse> SyncWeeklyJobsAsync(
        SchedulePlan plan,
        Action<ScheduleSyncProgress>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var progress = new SyncProgressReporter(onProgress);

        try
        {
            var apiBaseUrl = _configuration["ApiBaseUrl"]
                ?? throw new InvalidOperationException("ApiBaseUrl not configured");
            var apiKey = _configuration["ExternalSync:ApiKey"]
                ?? throw new InvalidOperationException("ExternalSync:ApiKey not configured");

            var gitHub = ReadGitHubConfig();
            var jobPrefix = BuildJobPrefix(gitHub.Environment);

            // sync-scores is the only job type that targets ApiBaseUrl directly — the rest go
            // via GitHub, which resolves the URL from the environment. So a wrong ApiBaseUrl
            // breaks score syncing alone, and silently: the jobs are created happily and only
            // fail later, against whichever API the URL actually points at.
            _logger.LogInformation(
                "Generating {Environment} jobs — sync-scores will call {SyncHost}, dispatches go to {Owner}/{Repo}",
                gitHub.Environment,
                Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var parsed) ? parsed.Host : apiBaseUrl,
                gitHub.Owner, gitHub.Repo);

            // Delete only THIS environment's existing jobs before creating new ones
            await DeleteExistingEplJobsAsync(jobPrefix, progress, cancellationToken);

            // Build the cron-jobs.org jobs for this week's plan
            var jobs = BuildJobRequests(plan, apiBaseUrl, apiKey, gitHub, jobPrefix);

            _logger.LogInformation("Creating {Count} cron-jobs.org jobs for {Scope}",
                jobs.Count, plan.Label);

            progress.SetJobsToCreate(jobs.Count);

            foreach (var job in jobs)
            {
                await _client.CreateJobAsync(job, cancellationToken);
                progress.JobCreated();
            }

            return new ScheduleGenerationResponse
            {
                Success = true,
                Message = $"Synced {progress.Created} jobs on cron-jobs.org for {plan.Label}",
                JobCount = progress.Created
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync weekly jobs on cron-jobs.org");
            return new ScheduleGenerationResponse
            {
                Success = false,
                Message = $"Failed to sync cron-jobs.org: {ex.Message}",
                JobCount = 0
            };
        }
    }

    private List<CronJobRequest> BuildJobRequests(
        SchedulePlan plan, string apiBaseUrl, string apiKey, GitHubDispatchConfig gitHub, string jobPrefix)
    {
        var requests = new List<CronJobRequest>();
        var index = new Dictionary<string, int>(); // title-part → counter for unique titles

        int Next(string key)
        {
            index.TryGetValue(key, out var n);
            index[key] = n + 1;
            return n + 1;
        }

        // Titles read "EPL-DEV-2026-27-GW1-auto-pick-1": the season and gameweek the job serves,
        // not the calendar week the plan happened to be generated in.
        string Scope(ScheduledJob job) => job.GameweekNumber is { } week
            ? $"{SchedulePlan.FormatSeason(job.SeasonId)}-GW{week}"
            : plan.IsoWeek;

        foreach (var job in plan.Jobs)
        {
            var scope = Scope(job);

            if (job.JobType == "sync-scores")
            {
                // 1. Direct sync job — stays on cron-job.org (hits the API directly).
                //    The key goes in a header, not the query string: URLs are written to
                //    request logs in full and are visible in cron-job.org's UI, which is how
                //    this key has leaked before.
                requests.Add(new CronJobRequest
                {
                    Title = $"{jobPrefix}{scope}-sync-scores-{Next($"{scope}-sync-scores")}",
                    Url = $"{apiBaseUrl}{SyncEndpoint}",
                    Schedule = BuildSchedule(job),
                    ExtendedData = new CronJobExtendedData
                    {
                        Headers = new Dictionary<string, string> { ["X-API-Key"] = apiKey },
                    },
                });

                // 2. One wake shortly before the window so the first sync hits a warm API.
                var wakeTime = job.ScheduledTime - SyncWakeLeadTime;
                requests.Add(BuildDispatchJob(
                    $"{jobPrefix}{scope}-wake-{Next($"{scope}-wake")}",
                    wakeTime, "wake", gitHub));
            }
            else if (DispatchActions.TryGetValue(job.JobType, out var action))
            {
                // Reminders / auto-pick run through GitHub Actions (wake + call).
                requests.Add(BuildDispatchJob(
                    $"{jobPrefix}{scope}-{job.JobType}-{Next($"{scope}-{job.JobType}")}",
                    job.ScheduledTime, action, gitHub));
            }
            else
            {
                _logger.LogWarning("Unknown job type: {JobType} — skipping", job.JobType);
            }
        }

        return requests;
    }

    /// <summary>
    /// Builds a cron-job.org job that POSTs to GitHub's repository_dispatch API, triggering
    /// the run-api-task workflow which wakes the Render API and calls the given action.
    /// </summary>
    private static CronJobRequest BuildDispatchJob(
        string title, DateTime scheduledTimeUtc, string action, GitHubDispatchConfig gitHub)
    {
        var url = $"https://api.github.com/repos/{gitHub.Owner}/{gitHub.Repo}/dispatches";

        var body = JsonSerializer.Serialize(new
        {
            event_type = gitHub.EventType,
            client_payload = new { environment = gitHub.Environment, action },
        });

        return new CronJobRequest
        {
            Title = title,
            Url = url,
            RequestMethod = 1, // POST
            Schedule = BuildOneOffSchedule(scheduledTimeUtc),
            ExtendedData = new CronJobExtendedData
            {
                Headers = new Dictionary<string, string>
                {
                    ["Authorization"] = $"Bearer {gitHub.Token}",
                    ["Accept"] = "application/vnd.github+json",
                    ["X-GitHub-Api-Version"] = "2022-11-28",
                    ["Content-Type"] = "application/json",
                    ["User-Agent"] = "cron-job.org",
                },
                Body = body,
            },
        };
    }

    private GitHubDispatchConfig ReadGitHubConfig() => new(
        Owner: _configuration["GitHub:Owner"]
            ?? throw new InvalidOperationException("GitHub:Owner not configured"),
        Repo: _configuration["GitHub:Repo"]
            ?? throw new InvalidOperationException("GitHub:Repo not configured"),
        Token: _configuration["GitHub:Token"]
            ?? throw new InvalidOperationException("GitHub:Token not configured"),
        Environment: _configuration["GitHub:Environment"]
            ?? throw new InvalidOperationException("GitHub:Environment not configured"),
        EventType: _configuration["GitHub:EventType"] ?? "run-api-task");

    private static CronJobSchedule BuildSchedule(ScheduledJob job)
    {
        if (job.IsRecurring && job.Interval.HasValue && job.EndTime.HasValue)
        {
            var intervalMinutes = (int)job.Interval.Value.TotalMinutes;

            // Generate the minutes array (e.g. every 2 min → [0,2,4,...,58])
            var minutes = Enumerable.Range(0, 60)
                .Where(m => m % intervalMinutes == 0)
                .ToArray();

            // Span the hours from start to end (inclusive)
            var startHour = job.ScheduledTime.Hour;
            var endHour = job.EndTime.Value.Hour;
            var hours = Enumerable.Range(startHour, endHour - startHour + 1).ToArray();

            return new CronJobSchedule
            {
                Hours = hours,
                Minutes = minutes,
                Mdays = [job.ScheduledTime.Day],
                Months = [job.ScheduledTime.Month],
                Wdays = [-1]
            };
        }

        return BuildOneOffSchedule(job.ScheduledTime);
    }

    private static CronJobSchedule BuildOneOffSchedule(DateTime timeUtc) => new()
    {
        Hours = [timeUtc.Hour],
        Minutes = [timeUtc.Minute],
        Mdays = [timeUtc.Day],
        Months = [timeUtc.Month],
        Wdays = [-1]
    };

    private async Task DeleteExistingEplJobsAsync(
        string jobPrefix, SyncProgressReporter progress, CancellationToken cancellationToken)
    {
        var allJobs = await _client.GetJobsAsync(cancellationToken);
        var eplJobs = allJobs.Where(j => j.Title.StartsWith(jobPrefix)).ToList();

        if (eplJobs.Count == 0)
        {
            _logger.LogDebug("No existing EPL cron jobs to remove");
            return;
        }

        _logger.LogInformation("Removing {Count} existing EPL cron jobs", eplJobs.Count);

        progress.SetJobsToDelete(eplJobs.Count);

        foreach (var j in eplJobs)
        {
            try
            {
                await _client.DeleteJobAsync(j.JobId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete cron job {JobId} ({Title}) — continuing", j.JobId, j.Title);
            }
            finally
            {
                // Counted either way: this is "jobs processed", so a job we failed to delete
                // does not stall the progress the caller is polling.
                progress.JobDeleted();
            }
        }
    }

    /// <summary>
    /// Accumulates sync counters and pushes a snapshot to the caller's callback after each step.
    /// </summary>
    private sealed class SyncProgressReporter(Action<ScheduleSyncProgress>? onProgress)
    {
        private int _toDelete;
        private int _deleted;
        private int _toCreate;

        public int Created { get; private set; }

        public void SetJobsToDelete(int count) { _toDelete = count; Report(); }
        public void SetJobsToCreate(int count) { _toCreate = count; Report(); }
        public void JobDeleted() { _deleted++; Report(); }
        public void JobCreated() { Created++; Report(); }

        private void Report() =>
            onProgress?.Invoke(new ScheduleSyncProgress(_deleted, _toDelete, Created, _toCreate));
    }

    private sealed record GitHubDispatchConfig(
        string Owner, string Repo, string Token, string Environment, string EventType);
}
