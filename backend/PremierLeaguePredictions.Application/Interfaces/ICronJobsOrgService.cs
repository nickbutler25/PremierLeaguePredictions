using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Interfaces;

public interface ICronJobsOrgService
{
    Task<ScheduleGenerationResponse> SyncWeeklyJobsAsync(SchedulePlan plan, CancellationToken cancellationToken = default);
}
