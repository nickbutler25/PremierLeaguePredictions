namespace PremierLeaguePredictions.Core.Entities;

public class Gameweek
{
    public string SeasonId { get; set; } = string.Empty; // References Season.Name
    public int WeekNumber { get; set; } // Composite Key with SeasonId
    public DateTime Deadline { get; set; }
    public bool IsLocked { get; set; } = false;
    public int EliminationCount { get; set; } = 0; // Number of players to eliminate after this gameweek
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Season Season { get; set; } = null!;
    public ICollection<Fixture> Fixtures { get; set; } = new List<Fixture>();
    public ICollection<Pick> Picks { get; set; } = new List<Pick>();
    public ICollection<EmailNotification> EmailNotifications { get; set; } = new List<EmailNotification>();
    public ICollection<AdminAction> AdminActions { get; set; } = new List<AdminAction>();
    public ICollection<UserElimination> Eliminations { get; set; } = new List<UserElimination>();
}
