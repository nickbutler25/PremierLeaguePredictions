namespace PremierLeaguePredictions.Application.DTOs;

/// <summary>
/// Represents a plan of scheduled jobs to be created for the week
/// </summary>
public class SchedulePlan
{
    public List<ScheduledJob> Jobs { get; set; } = new();
    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Human-readable summary of what this plan covers, e.g. "2026-27 GW1". Used in job titles
    /// and log messages. Falls back to the ISO calendar week when the plan has no gameweeks,
    /// which only happens off-season when there is nothing to schedule.
    /// </summary>
    public string Label
    {
        get
        {
            var gameweeks = Jobs
                .Where(j => j.GameweekNumber.HasValue)
                .Select(j => $"{FormatSeason(j.SeasonId)}-GW{j.GameweekNumber}")
                .Distinct()
                .ToList();

            return gameweeks.Count > 0 ? string.Join("+", gameweeks) : IsoWeek;
        }
    }

    /// <summary>ISO calendar week the plan was built in, e.g. "2026-W34".</summary>
    public string IsoWeek => $"{StartDate.Year}-W{System.Globalization.ISOWeek.GetWeekOfYear(StartDate):D2}";

    /// <summary>
    /// Turns a season name into a label safe for a cron-job.org job title. Only the slash
    /// needs replacing: "2026/2027" and "2026-2027" both become "2026-2027".
    /// </summary>
    public static string FormatSeason(string seasonId) => seasonId.Replace('/', '-');

    public void AddJob(DateTime scheduledTime, string jobType, string seasonId, int gameweekNumber)
    {
        Jobs.Add(new ScheduledJob
        {
            ScheduledTime = scheduledTime,
            JobType = jobType,
            SeasonId = seasonId,
            GameweekNumber = gameweekNumber,
            IsRecurring = false
        });
    }

    public void AddRecurringJob(
        DateTime startTime, DateTime endTime, TimeSpan interval, string jobType,
        string seasonId, int gameweekNumber)
    {
        Jobs.Add(new ScheduledJob
        {
            ScheduledTime = startTime,
            EndTime = endTime,
            Interval = interval,
            JobType = jobType,
            SeasonId = seasonId,
            GameweekNumber = gameweekNumber,
            IsRecurring = true
        });
    }
}

/// <summary>
/// Represents a single scheduled job
/// </summary>
public class ScheduledJob
{
    /// <summary>
    /// The UTC time when this job should run (or start, if recurring)
    /// </summary>
    public DateTime ScheduledTime { get; set; }

    /// <summary>
    /// For recurring jobs, the UTC time when the job should stop running
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// For recurring jobs, how often the job should run
    /// </summary>
    public TimeSpan? Interval { get; set; }

    /// <summary>
    /// Type of job: "send-reminders", "auto-pick", "sync-scores"
    /// </summary>
    public string JobType { get; set; } = string.Empty;

    /// <summary>
    /// Season this job belongs to, e.g. "2026/2027" (matches Season.Name)
    /// </summary>
    public string SeasonId { get; set; } = string.Empty;

    /// <summary>
    /// Gameweek this job belongs to, e.g. 1
    /// </summary>
    public int? GameweekNumber { get; set; }

    /// <summary>
    /// Whether this is a one-time or recurring job
    /// </summary>
    public bool IsRecurring { get; set; }

    /// <summary>
    /// Generate cron expression for this job
    /// </summary>
    public string ToCronExpression()
    {
        if (IsRecurring && Interval.HasValue)
        {
            // Format: */interval hour-range day month *
            // Example: */2 15-17 6 12 * (every 2 min, 3-5 PM, Dec 6)
            var intervalMinutes = (int)Interval.Value.TotalMinutes;

            if (EndTime.HasValue && ScheduledTime.Day == EndTime.Value.Day)
            {
                // Same day - use hour range
                return $"*/{intervalMinutes} {ScheduledTime.Hour}-{EndTime.Value.Hour} {ScheduledTime.Day} {ScheduledTime.Month} *";
            }
            else
            {
                // Different days or no end time - just use start time
                return $"*/{intervalMinutes} {ScheduledTime.Hour} {ScheduledTime.Day} {ScheduledTime.Month} *";
            }
        }
        else
        {
            // One-time job: minute hour day month *
            return $"{ScheduledTime.Minute} {ScheduledTime.Hour} {ScheduledTime.Day} {ScheduledTime.Month} *";
        }
    }

    /// <summary>
    /// Get a unique identifier for this job schedule (for use in workflow if conditions)
    /// </summary>
    public string GetScheduleId()
    {
        return ToCronExpression();
    }
}

/// <summary>
/// Response from generating a weekly schedule
/// </summary>
public class ScheduleGenerationResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int JobCount { get; set; }
}
