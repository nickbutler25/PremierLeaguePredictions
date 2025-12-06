namespace PremierLeaguePredictions.Application.DTOs;

/// <summary>
/// Represents a plan of scheduled jobs to be created for the week
/// </summary>
public class SchedulePlan
{
    public List<ScheduledJob> Jobs { get; set; } = new();

    public void AddJob(DateTime scheduledTime, string jobType, string? gameweekId = null)
    {
        Jobs.Add(new ScheduledJob
        {
            ScheduledTime = scheduledTime,
            JobType = jobType,
            GameweekId = gameweekId,
            IsRecurring = false
        });
    }

    public void AddRecurringJob(DateTime startTime, DateTime endTime, TimeSpan interval, string jobType, string? gameweekId = null)
    {
        Jobs.Add(new ScheduledJob
        {
            ScheduledTime = startTime,
            EndTime = endTime,
            Interval = interval,
            JobType = jobType,
            GameweekId = gameweekId,
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
    /// Optional gameweek ID if this job is specific to a gameweek
    /// </summary>
    public string? GameweekId { get; set; }

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
    public string Message { get; set; } = string.Empty;
    public int JobsCreated { get; set; }
    public string? WorkflowFilePath { get; set; }
    public List<string> ScheduledJobTypes { get; set; } = new();
}
