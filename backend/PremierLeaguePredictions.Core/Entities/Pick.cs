namespace PremierLeaguePredictions.Core.Entities;

public class Pick
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string SeasonId { get; set; } = string.Empty; // References Season.Name
    public int GameweekNumber { get; set; } // References Gameweek.WeekNumber
    public int TeamId { get; set; }
    public int Points { get; set; } = 0;
    public int GoalsFor { get; set; } = 0;
    public int GoalsAgainst { get; set; } = 0;
    public bool IsAutoAssigned { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Gameweek Gameweek { get; set; } = null!;
    public Team Team { get; set; } = null!;
}
