using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Application.Services;

public class CronSchedulerService : ICronSchedulerService
{
    /// <summary>
    /// How long after a gameweek's last kickoff to try completing it. A match plus stoppage runs
    /// about two hours, and the score sync window closes at kickoff + 2h — the extra half hour
    /// lets that final sync land before the gameweek is judged finished.
    /// </summary>
    private static readonly TimeSpan CompletionDelayAfterLastKickoff = TimeSpan.FromHours(2.5);

    /// <summary>
    /// Delay used when a gameweek's football is already over but it is still open, so the job is
    /// a retry rather than a first attempt.
    /// </summary>
    private static readonly TimeSpan CompletionRetryDelay = TimeSpan.FromMinutes(30);

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CronSchedulerService> _logger;

    public CronSchedulerService(
        IUnitOfWork unitOfWork,
        ILogger<CronSchedulerService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// The season jobs may be generated for. Null when there is none, which means no jobs.
    /// </summary>
    private async Task<string?> GetActiveSeasonNameAsync(CancellationToken cancellationToken) =>
        (await _unitOfWork.Seasons.FindAsync(s => s.IsActive, cancellationToken))
        .FirstOrDefault()?.Name;

    public async Task<SchedulePlan> GenerateWeeklyScheduleAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var nextWeek = now.AddDays(7);

        _logger.LogInformation("Generating weekly schedule from {StartDate} to {EndDate}",
            now, nextWeek);

        var plan = new SchedulePlan();

        // Only gameweeks in play over the next 7 days. The deadline sits just before the
        // gameweek's first kickoff, so a window of [now - 7d, now + 7d] on the deadline covers
        // both the gameweek whose deadline is coming up and the one currently in progress
        // (deadline just passed, fixtures still to be synced).
        //
        // Without this bound, EVERY unlocked gameweek in the season qualifies — each one's
        // reminders (24h/12h/3h before deadline) and auto-pick are all "in the future" — which
        // produced 158 jobs for a full season and hit cron-job.org's API rate limit.
        var windowStart = now.AddDays(-7);

        // Scoped to the active season. A gameweek belonging to any other season - a finished
        // one, or a leftover test season - must never be given jobs: on 2026-09-08 an E2E-TEST
        // season's week 1 fell inside this window and was handed the full set, so real players
        // got a 3h reminder and then an auto-pick email for a gameweek in a competition nobody
        // was playing. !IsLocked is not a substitute for this; nothing had locked that gameweek
        // and nothing was going to.
        var activeSeason = await GetActiveSeasonNameAsync(cancellationToken);
        if (activeSeason == null)
        {
            _logger.LogWarning("No active season; generating an empty schedule");
            return plan;
        }

        var allGameweeks = await _unitOfWork.Gameweeks.FindAsync(
            g => g.SeasonId == activeSeason
                 && !g.IsLocked // Only get gameweeks that aren't finalized yet
                 && g.Deadline >= windowStart
                 && g.Deadline <= nextWeek,
            cancellationToken);

        var gameweeksList = allGameweeks.OrderBy(g => g.Deadline).ToList();

        _logger.LogInformation(
            "Found {Count} unlocked gameweeks with deadlines between {WindowStart} and {WindowEnd}",
            gameweeksList.Count, windowStart, nextWeek);

        foreach (var gameweek in gameweeksList)
        {
            _logger.LogInformation("Processing gameweek {SeasonId}-{WeekNumber}, deadline: {Deadline}",
                gameweek.SeasonId, gameweek.WeekNumber, gameweek.Deadline);

            // Schedule reminder emails: 24h and 3h before deadline. These must match
            // PickReminderService.ReminderWindows — a job firing outside a window wakes the
            // API and sends nothing.
            var reminder24h = gameweek.Deadline.AddHours(-24);
            var reminder3h = gameweek.Deadline.AddHours(-3);

            // Only schedule reminders that are in the future
            if (reminder24h > now)
            {
                plan.AddJob(reminder24h, "send-reminders", gameweek.SeasonId, gameweek.WeekNumber);
                _logger.LogDebug("Scheduled 24h reminder for {Time}", reminder24h);
            }

            if (reminder3h > now)
            {
                plan.AddJob(reminder3h, "send-reminders", gameweek.SeasonId, gameweek.WeekNumber);
                _logger.LogDebug("Scheduled 3h reminder for {Time}", reminder3h);
            }

            // Schedule auto-pick assignment at deadline
            if (gameweek.Deadline > now)
            {
                plan.AddJob(gameweek.Deadline, "auto-pick", gameweek.SeasonId, gameweek.WeekNumber);
                _logger.LogDebug("Scheduled auto-pick for {Time}", gameweek.Deadline);
            }

            // Schedule live score syncs for this gameweek's fixtures
            var fixtures = await _unitOfWork.Fixtures.FindAsync(
                f => f.SeasonId == gameweek.SeasonId && f.GameweekNumber == gameweek.WeekNumber,
                cancellationToken);

            // Include fixtures that:
            // 1. Haven't started yet and kick off in next 7 days, OR
            // 2. Are in progress (kicked off within last 2 hours)
            var syncWindowStart = now.AddHours(-2); // Matches sync for 2h after kickoff

            var fixturesList = fixtures
                .Where(f => f.KickoffTime >= syncWindowStart && f.KickoffTime <= nextWeek)
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

                // Schedule if:
                // 1. Match hasn't started yet (syncStart > now), OR
                // 2. Match is in progress (now is between syncStart and syncEnd)
                if (syncEnd > now)
                {
                    // If match already started, begin syncing from now instead of kickoff
                    var effectiveSyncStart = syncStart > now ? syncStart : now;

                    plan.AddRecurringJob(
                        effectiveSyncStart,
                        syncEnd,
                        TimeSpan.FromMinutes(2),
                        "sync-scores",
                        gameweek.SeasonId,
                        gameweek.WeekNumber
                    );

                    if (syncStart <= now)
                    {
                        _logger.LogInformation(
                            "Scheduled live score sync for {Count} in-progress match(es) from now until {EndTime}",
                            matchCount, syncEnd);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Scheduled live score sync for {Count} match(es) at {KickoffTime} (every 2 min until {EndTime})",
                            matchCount, kickoffWindow, syncEnd);
                    }
                }
                else
                {
                    _logger.LogDebug(
                        "Skipped {Count} match(es) at {KickoffTime} - sync window has already ended",
                        matchCount, kickoffWindow);
                }
            }
        }

        await AddGameweekCompletionJobsAsync(plan, now, nextWeek, cancellationToken);

        _logger.LogInformation("Schedule generation complete. Total jobs: {JobCount}", plan.Jobs.Count);

        // Log summary by job type
        var reminderJobs = plan.Jobs.Count(j => j.JobType == "send-reminders");
        var autoPickJobs = plan.Jobs.Count(j => j.JobType == "auto-pick");
        var syncJobs = plan.Jobs.Count(j => j.JobType == "sync-scores");
        var completionJobs = plan.Jobs.Count(j => j.JobType == "complete-gameweek");

        _logger.LogInformation(
            "Job summary: {Reminders} reminders, {AutoPicks} auto-picks, {Syncs} score syncs, {Completions} completions",
            reminderJobs, autoPickJobs, syncJobs, completionJobs);

        return plan;
    }

    /// <summary>
    /// Schedules a completion job for each gameweek that still needs finalising.
    /// </summary>
    /// <remarks>
    /// Covers two cases with one rule. Normally it is the gameweek about to finish, timed shortly
    /// after its last kickoff. But it also picks up any gameweek whose deadline has passed and
    /// which is still unlocked — a completion that was skipped because a fixture was postponed.
    /// That is what makes the schedule self-healing: each weekly run gives a stranded gameweek
    /// another attempt, and once the postponed match is replayed the attempt succeeds.
    /// </remarks>
    private async Task AddGameweekCompletionJobsAsync(
        SchedulePlan plan, DateTime now, DateTime nextWeek, CancellationToken cancellationToken)
    {
        // A gameweek qualifies once its deadline is behind us or comes within the planning
        // window. Anything further out is a future gameweek with nothing to complete.
        var activeSeason = await GetActiveSeasonNameAsync(cancellationToken);
        if (activeSeason == null)
        {
            return;
        }

        var openGameweeks = (await _unitOfWork.Gameweeks.FindAsync(
                g => g.SeasonId == activeSeason && !g.IsLocked && g.Deadline <= nextWeek,
                cancellationToken))
            .OrderBy(g => g.Deadline)
            .ToList();

        foreach (var gameweek in openGameweeks)
        {
            var fixtures = await _unitOfWork.Fixtures.FindAsync(
                f => f.SeasonId == gameweek.SeasonId && f.GameweekNumber == gameweek.WeekNumber,
                cancellationToken);

            var fixtureList = fixtures.ToList();
            if (fixtureList.Count == 0)
            {
                _logger.LogDebug("GW{WeekNumber} has no fixtures — no completion job", gameweek.WeekNumber);
                continue;
            }

            var lastKickoff = fixtureList.Max(f => f.KickoffTime);
            var scheduledTime = lastKickoff.Add(CompletionDelayAfterLastKickoff);

            if (scheduledTime <= now)
            {
                // The football is already over and the gameweek is still open, so this is a retry
                // of a completion that did not take. Run it shortly after this generate rather
                // than waiting another week.
                scheduledTime = now.Add(CompletionRetryDelay);

                _logger.LogInformation(
                    "GW{WeekNumber} is past its last kickoff but still open — scheduling a retry at {Time}",
                    gameweek.WeekNumber, scheduledTime);
            }
            else if (scheduledTime > nextWeek)
            {
                // Its last match falls outside this planning window; next week's run will cover it.
                continue;
            }

            plan.AddJob(scheduledTime, "complete-gameweek", gameweek.SeasonId, gameweek.WeekNumber);

            _logger.LogInformation("Scheduled gameweek completion for GW{WeekNumber} at {Time} (last kickoff {Kickoff})",
                gameweek.WeekNumber, scheduledTime, lastKickoff);
        }
    }
}
