using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Interfaces;

/// <summary>
/// Runs schedule generation in the background so the triggering request can return
/// immediately. Generation paces its calls to cron-job.org to stay under that API's rate
/// limit, which makes it far too slow to hold an HTTP request open for.
/// </summary>
public interface IScheduleGenerationRunner
{
    /// <summary>
    /// Starts a run if none is in progress. Returns false (with the in-progress run's status)
    /// when one is already running — concurrent runs would delete each other's jobs.
    /// </summary>
    bool TryStart(out ScheduleGenerationStatus status);

    /// <summary>
    /// Status of the current or most recent run, or null if none has been started since
    /// the process last restarted.
    /// </summary>
    ScheduleGenerationStatus? GetStatus();
}
