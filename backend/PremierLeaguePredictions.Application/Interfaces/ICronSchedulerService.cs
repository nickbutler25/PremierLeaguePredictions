using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Interfaces;

/// <summary>
/// Service for generating weekly cron job schedules based on upcoming gameweeks
/// </summary>
public interface ICronSchedulerService
{
    /// <summary>
    /// Generates a schedule plan for the next 7 days
    /// Includes reminder jobs, auto-pick jobs, and live score sync jobs
    /// </summary>
    Task<SchedulePlan> GenerateWeeklyScheduleAsync(CancellationToken cancellationToken = default);
}
