namespace PremierLeaguePredictions.Core.Entities;

public class Fixture
{
    public Guid Id { get; set; }
    public Guid GameweekId { get; set; }
    public Guid HomeTeamId { get; set; }
    public Guid AwayTeamId { get; set; }
    public DateTime KickoffTime { get; set; }
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public string Status { get; set; } = "SCHEDULED"; // SCHEDULED, IN_PLAY, FINISHED, POSTPONED, CANCELLED
    public int? ExternalId { get; set; } // Football Data API ID
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Gameweek Gameweek { get; set; } = null!;
    public Team HomeTeam { get; set; } = null!;
    public Team AwayTeam { get; set; } = null!;
}
