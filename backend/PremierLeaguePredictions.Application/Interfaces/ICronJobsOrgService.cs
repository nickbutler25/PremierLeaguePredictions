using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Interfaces;

public interface ICronJobsOrgService
{
    /// <summary>
    /// Replaces this environment's jobs on cron-jobs.org with the given plan.
    /// </summary>
    /// <param name="onProgress">
    /// Called synchronously after each job is deleted or created. Calls are paced to stay
    /// under the cron-job.org rate limit, so a full sync takes seconds per job.
    /// </param>
    Task<ScheduleGenerationResponse> SyncWeeklyJobsAsync(
        SchedulePlan plan,
        Action<ScheduleSyncProgress>? onProgress = null,
        CancellationToken cancellationToken = default);
}
