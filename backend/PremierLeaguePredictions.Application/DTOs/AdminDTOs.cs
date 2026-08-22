namespace PremierLeaguePredictions.Application.DTOs;

public class AdminActionDto
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class BackfillPickRequest
{
    public int GameweekNumber { get; set; }
    public int TeamId { get; set; }
}

public class BackfillPicksResponse
{
    public int PicksCreated { get; set; }
    public int PicksUpdated { get; set; }
    public int PicksSkipped { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ReminderResult
{
    public int EmailsSent { get; set; }
    public int EmailsFailed { get; set; }

    /// <summary>
    /// Recipients deliberately not mailed because their address is a non-deliverable test one.
    /// Counted separately so they never inflate EmailsSent.
    /// </summary>
    public int EmailsSkipped { get; set; }

    public bool Success => EmailsFailed == 0;

    public string Message
    {
        get
        {
            var skipped = EmailsSkipped > 0 ? $" ({EmailsSkipped} test address(es) skipped)" : "";

            return EmailsFailed == 0
                ? $"Successfully sent {EmailsSent} reminder email(s){skipped}"
                : $"Sent {EmailsSent} reminder(s), but {EmailsFailed} failed{skipped}";
        }
    }
}

public class AutoPickResult
{
    public int PicksAssigned { get; set; }
    public int PicksFailed { get; set; }
    public int GameweeksProcessed { get; set; }
    public bool Success => PicksFailed == 0;
    public string Message => PicksFailed == 0
        ? $"Successfully assigned {PicksAssigned} auto-pick(s) across {GameweeksProcessed} gameweek(s)"
        : $"Assigned {PicksAssigned} auto-pick(s), but {PicksFailed} failed";
}
