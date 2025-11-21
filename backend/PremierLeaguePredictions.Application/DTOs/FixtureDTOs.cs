namespace PremierLeaguePredictions.Application.DTOs;

public class FixtureDto
{
    public Guid Id { get; set; }
    public Guid GameweekId { get; set; }
    public int GameweekNumber { get; set; }
    public Guid HomeTeamId { get; set; }
    public Guid AwayTeamId { get; set; }
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
    public Guid GameweekId { get; set; }
    public Guid HomeTeamId { get; set; }
    public Guid AwayTeamId { get; set; }
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
