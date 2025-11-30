namespace PremierLeaguePredictions.Core.Entities;

public class Fixture
{
    public Guid Id { get; set; }
    public string SeasonId { get; set; } = string.Empty; // References Season.Name
    public int GameweekNumber { get; set; } // References Gameweek.WeekNumber
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
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
