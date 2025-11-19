namespace PremierLeaguePredictions.API.Models;

public class EmailNotification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid GameweekId { get; set; }
    public string EmailType { get; set; } = string.Empty; // PICK_REMINDER, GAMEWEEK_STARTED, etc.
    public DateTime SentAt { get; set; }
    public string Status { get; set; } = "SENT"; // SENT, FAILED, PENDING
    public string? ErrorMessage { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Gameweek Gameweek { get; set; } = null!;
}
