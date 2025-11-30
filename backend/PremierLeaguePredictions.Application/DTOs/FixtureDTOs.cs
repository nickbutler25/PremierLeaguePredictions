namespace PremierLeaguePredictions.Application.DTOs;

public class FixtureDto
{
    public Guid Id { get; set; }
    public string SeasonId { get; set; } = string.Empty;
    public int GameweekNumber { get; set; }
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public DateTime KickoffTime { get; set; }
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public string Status { get; set; } = "SCHEDULED";
    public int? ExternalApiId { get; set; }

    public TeamDto? HomeTeam { get; set; }
    public TeamDto? AwayTeam { get; set; }
}

public class CreateFixtureRequest
{
    public string SeasonId { get; set; } = string.Empty;
    public int GameweekNumber { get; set; }
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public DateTime KickoffTime { get; set; }
    public int? ExternalApiId { get; set; }
}

public class UpdateFixtureRequest
{
    public DateTime? KickoffTime { get; set; }
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public string? Status { get; set; }
}
