using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Application.Services;

public class CronSchedulerService : ICronSchedulerService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CronSchedulerService> _logger;

    public CronSchedulerService(
        IUnitOfWork unitOfWork,
        ILogger<CronSchedulerService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<SchedulePlan> GenerateWeeklyScheduleAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var nextWeek = now.AddDays(7);

        _logger.LogInformation("Generating weekly schedule from {StartDate} to {EndDate}",
            now, nextWeek);

        var plan = new SchedulePlan();

        // Get gameweeks with deadlines in the next 7 days
        var upcomingGameweeks = await _unitOfWork.Gameweeks.FindAsync(
            g => g.Deadline >= now && g.Deadline <= nextWeek,
            cancellationToken);

        var gameweeksList = upcomingGameweeks.OrderBy(g => g.Deadline).ToList();

        _logger.LogInformation("Found {Count} upcoming gameweeks", gameweeksList.Count);

        foreach (var gameweek in gameweeksList)
        {
            _logger.LogInformation("Processing gameweek {SeasonId}-{WeekNumber}, deadline: {Deadline}",
                gameweek.SeasonId, gameweek.WeekNumber, gameweek.Deadline);

            // Schedule reminder emails: 24h, 12h, 3h before deadline
            var reminder24h = gameweek.Deadline.AddHours(-24);
            var reminder12h = gameweek.Deadline.AddHours(-12);
            var reminder3h = gameweek.Deadline.AddHours(-3);

            // Only schedule reminders that are in the future
            if (reminder24h > now)
            {
                plan.AddJob(reminder24h, "send-reminders");
                _logger.LogDebug("Scheduled 24h reminder for {Time}", reminder24h);
            }

            if (reminder12h > now)
            {
                plan.AddJob(reminder12h, "send-reminders");
                _logger.LogDebug("Scheduled 12h reminder for {Time}", reminder12h);
            }

            if (reminder3h > now)
            {
                plan.AddJob(reminder3h, "send-reminders");
                _logger.LogDebug("Scheduled 3h reminder for {Time}", reminder3h);
            }

            // Schedule auto-pick assignment at deadline
            if (gameweek.Deadline > now)
            {
                plan.AddJob(gameweek.Deadline, "auto-pick");
                _logger.LogDebug("Scheduled auto-pick for {Time}", gameweek.Deadline);
            }

            // Schedule live score syncs for this gameweek's fixtures
            var fixtures = await _unitOfWork.Fixtures.FindAsync(
                f => f.SeasonId == gameweek.SeasonId && f.GameweekNumber == gameweek.WeekNumber,
                cancellationToken);

            var fixturesList = fixtures
                .Where(f => f.KickoffTime >= now && f.KickoffTime <= nextWeek)
                .OrderBy(f => f.KickoffTime)
                .ToList();

            _logger.LogInformation("Found {Count} fixtures for gameweek {WeekNumber}",
                fixturesList.Count, gameweek.WeekNumber);

            // Group fixtures by kickoff time (rounded to nearest 15 minutes)
            var fixtureGroups = fixturesList.GroupBy(f =>
                new DateTime(
                    f.KickoffTime.Year,
                    f.KickoffTime.Month,
                    f.KickoffTime.Day,
                    f.KickoffTime.Hour,
                    (f.KickoffTime.Minute / 15) * 15,  // Round to 15-min intervals
                    0,
                    DateTimeKind.Utc
                )
            );

            foreach (var group in fixtureGroups)
            {
                var kickoffWindow = group.Key;
                var matchCount = group.Count();

                // Live score sync: every 2 minutes from kickoff to 2 hours after
                var syncStart = kickoffWindow;
                var syncEnd = kickoffWindow.AddHours(2);

                // Only schedule if it's in the future
                if (syncStart > now)
                {
                    plan.AddRecurringJob(
                        syncStart,
                        syncEnd,
                        TimeSpan.FromMinutes(2),
                        "sync-scores"
                    );

                    _logger.LogInformation(
                        "Scheduled live score sync for {Count} match(es) at {KickoffTime} (every 2 min until {EndTime})",
                        matchCount, kickoffWindow, syncEnd);
                }
            }
        }

        _logger.LogInformation("Schedule generation complete. Total jobs: {JobCount}", plan.Jobs.Count);

        // Log summary by job type
        var reminderJobs = plan.Jobs.Count(j => j.JobType == "send-reminders");
        var autoPickJobs = plan.Jobs.Count(j => j.JobType == "auto-pick");
        var syncJobs = plan.Jobs.Count(j => j.JobType == "sync-scores");

        _logger.LogInformation("Job summary: {Reminders} reminders, {AutoPicks} auto-picks, {Syncs} score syncs",
            reminderJobs, autoPickJobs, syncJobs);

        return plan;
    }
}
