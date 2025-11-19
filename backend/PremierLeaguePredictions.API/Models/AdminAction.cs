namespace PremierLeaguePredictions.API.Models;

public class AdminAction
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }
    public string ActionType { get; set; } = string.Empty; // OVERRIDE_PICK, OVERRIDE_DEADLINE, DEACTIVATE_USER, etc.
    public Guid? TargetUserId { get; set; }
    public Guid? TargetGameweekId { get; set; }
    public string? Details { get; set; } // JSON string for additional details
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public User AdminUser { get; set; } = null!;
    public User? TargetUser { get; set; }
    public Gameweek? TargetGameweek { get; set; }
}
