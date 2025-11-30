namespace PremierLeaguePredictions.Core.Entities;

public class TeamSelection
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string SeasonId { get; set; } = string.Empty;
    public int TeamId { get; set; }
    public int Half { get; set; } // 1 for weeks 1-20, 2 for weeks 21-38
    public int GameweekNumber { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Season Season { get; set; } = null!;
    public Team Team { get; set; } = null!;
}
