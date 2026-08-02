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

    // Jobs created by this app are prefixed so they can be found and cleaned up
    private const string JobTitlePrefix = "EPL-";

    private static readonly Dictionary<string, string> JobEndpoints = new()
    {
        ["send-reminders"] = "/api/v1/admin/schedule/reminders",
        ["auto-pick"]      = "/api/v1/admin/schedule/auto-pick",
        ["sync-scores"]    = "/api/v1/admin/sync/results",
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
        CancellationToken cancellationToken = default)
    {
        try
        {
            var apiBaseUrl = _configuration["ApiBaseUrl"]
                ?? throw new InvalidOperationException("ApiBaseUrl not configured");
            var apiKey = _configuration["ExternalSync:ApiKey"]
                ?? throw new InvalidOperationException("ExternalSync:ApiKey not configured");

            // Delete all existing EPL-prefixed jobs before creating new ones
            await DeleteExistingEplJobsAsync(cancellationToken);

            // Build one cron-jobs.org job per unique (jobType, schedule) combination
            var jobs = BuildJobRequests(plan, apiBaseUrl, apiKey);

            _logger.LogInformation("Creating {Count} cron-jobs.org jobs for week {Week}",
                jobs.Count, plan.WeekNumber);

            var created = 0;
            foreach (var job in jobs)
            {
                await _client.CreateJobAsync(job, cancellationToken);
                created++;
            }

            return new ScheduleGenerationResponse
            {
                Success = true,
                Message = $"Synced {created} jobs on cron-jobs.org for week {plan.WeekNumber}",
                WorkflowFile = null,
                JobCount = created
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

    private List<CronJobRequest> BuildJobRequests(SchedulePlan plan, string apiBaseUrl, string apiKey)
    {
        var requests = new List<CronJobRequest>();
        var index = new Dictionary<string, int>(); // job type → counter for unique titles

        foreach (var job in plan.Jobs)
        {
            if (!JobEndpoints.TryGetValue(job.JobType, out var endpoint))
            {
                _logger.LogWarning("Unknown job type: {JobType} — skipping", job.JobType);
                continue;
            }

            index.TryGetValue(job.JobType, out var n);
            index[job.JobType] = n + 1;

            var title = $"{JobTitlePrefix}{plan.WeekNumber}-{job.JobType}-{n + 1}";

            // Embed the API key as a query parameter — cron-job.org free tier does not support
            // custom request headers via extendedData, so auth is passed in the URL instead.
            var url = $"{apiBaseUrl}{endpoint}?apiKey={Uri.EscapeDataString(apiKey)}";

            requests.Add(new CronJobRequest
            {
                Title = title,
                Url = url,
                Schedule = BuildSchedule(job),
            });
        }

        return requests;
    }

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
        else
        {
            return new CronJobSchedule
            {
                Hours = [job.ScheduledTime.Hour],
                Minutes = [job.ScheduledTime.Minute],
                Mdays = [job.ScheduledTime.Day],
                Months = [job.ScheduledTime.Month],
                Wdays = [-1]
            };
        }
    }

    private async Task DeleteExistingEplJobsAsync(CancellationToken cancellationToken)
    {
        var allJobs = await _client.GetJobsAsync(cancellationToken);
        var eplJobs = allJobs.Where(j => j.Title.StartsWith(JobTitlePrefix)).ToList();

        if (eplJobs.Count == 0)
        {
            _logger.LogDebug("No existing EPL cron jobs to remove");
            return;
        }

        _logger.LogInformation("Removing {Count} existing EPL cron jobs", eplJobs.Count);

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
        }
    }
}
