namespace PremierLeaguePredictions.Core.Entities;

public class Gameweek
{
    public Guid Id { get; set; }
    public Guid SeasonId { get; set; }
    public int WeekNumber { get; set; }
    public DateTime Deadline { get; set; }
    public bool IsLocked { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Season Season { get; set; } = null!;
    public ICollection<Fixture> Fixtures { get; set; } = new List<Fixture>();
    public ICollection<Pick> Picks { get; set; } = new List<Pick>();
    public ICollection<EmailNotification> EmailNotifications { get; set; } = new List<EmailNotification>();
    public ICollection<AdminAction> AdminActions { get; set; } = new List<AdminAction>();
}
