using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;

namespace PremierLeaguePredictions.Application.Services;

/// <summary>
/// Owns the single in-flight schedule generation run. Registered as a singleton: state lives
/// in memory, which is enough because the API runs as one instance and a run only needs to
/// outlive the request that started it, not a restart.
/// </summary>
public class ScheduleGenerationRunner : IScheduleGenerationRunner
{
    /// <summary>
    /// Ceiling on a single run, so a hung cron-job.org call can never wedge the runner and
    /// block every later generate with a permanent "already running".
    /// </summary>
    private static readonly TimeSpan MaxRunDuration = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduleGenerationRunner> _logger;
    private readonly Lock _gate = new();

    private ScheduleGenerationStatus? _current;

    public ScheduleGenerationRunner(
        IServiceScopeFactory scopeFactory,
        ILogger<ScheduleGenerationRunner> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public ScheduleGenerationStatus? GetStatus()
    {
        lock (_gate)
        {
            return _current?.Clone();
        }
    }

    public bool TryStart(out ScheduleGenerationStatus status)
    {
        lock (_gate)
        {
            if (_current is { IsComplete: false })
            {
                status = _current.Clone();
                return false;
            }

            _current = new ScheduleGenerationStatus
            {
                RunId = Guid.NewGuid(),
                State = ScheduleRunState.Running,
                StartedAt = DateTime.UtcNow,
                Message = "Schedule generation started"
            };
            status = _current.Clone();
        }

        // Deliberately not awaited: the caller returns 202 straight away and polls the status
        // endpoint. Exceptions are captured onto the status inside RunAsync.
        var runId = status.RunId;
        _ = Task.Run(() => RunAsync(runId));
        return true;
    }

    private async Task RunAsync(Guid runId)
    {
        using var cts = new CancellationTokenSource(MaxRunDuration);

        // A fresh scope: the request that started this run — and its scoped DbContext — is
        // long gone by now.
        using var scope = _scopeFactory.CreateScope();

        try
        {
            _logger.LogInformation("Starting weekly schedule generation (run {RunId})", runId);

            var scheduler = scope.ServiceProvider.GetRequiredService<ICronSchedulerService>();
            var cronJobs = scope.ServiceProvider.GetRequiredService<ICronJobsOrgService>();

            var plan = await scheduler.GenerateWeeklyScheduleAsync(cts.Token);

            _logger.LogInformation("Generated schedule plan with {JobCount} jobs for {Scope} (run {RunId})",
                plan.Jobs.Count, plan.Label, runId);

            Update(runId, s =>
            {
                s.Scope = plan.Label;
                s.JobsPlanned = plan.Jobs.Count;
            });

            var result = await cronJobs.SyncWeeklyJobsAsync(
                plan,
                progress => Update(runId, s =>
                {
                    s.JobsToDelete = progress.JobsToDelete;
                    s.JobsDeleted = progress.JobsDeleted;
                    s.JobsToCreate = progress.JobsToCreate;
                    s.JobsCreated = progress.JobsCreated;
                }),
                cts.Token);

            Update(runId, s =>
            {
                s.State = result.Success ? ScheduleRunState.Succeeded : ScheduleRunState.Failed;
                s.Message = result.Message;
                s.CompletedAt = DateTime.UtcNow;
            });

            if (result.Success)
                _logger.LogInformation("Successfully synced {JobCount} jobs to cron-jobs.org (run {RunId})",
                    result.JobCount, runId);
            else
                _logger.LogError("Failed to sync jobs to cron-jobs.org: {Message} (run {RunId})",
                    result.Message, runId);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            _logger.LogError("Schedule generation run {RunId} timed out after {Minutes} minutes",
                runId, MaxRunDuration.TotalMinutes);

            Update(runId, s =>
            {
                s.State = ScheduleRunState.Failed;
                s.Message = $"Timed out after {MaxRunDuration.TotalMinutes} minutes";
                s.CompletedAt = DateTime.UtcNow;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating weekly schedule (run {RunId})", runId);

            Update(runId, s =>
            {
                s.State = ScheduleRunState.Failed;
                s.Message = $"Failed to generate schedule: {ex.Message}";
                s.CompletedAt = DateTime.UtcNow;
            });
        }
    }

    /// <summary>
    /// Applies an update, ignoring it if a newer run has since taken over.
    /// </summary>
    private void Update(Guid runId, Action<ScheduleGenerationStatus> apply)
    {
        lock (_gate)
        {
            if (_current?.RunId == runId)
                apply(_current);
        }
    }
}
