namespace PremierLeaguePredictions.Application.DTOs;

public class TeamDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Code { get; set; }
    public string? LogoUrl { get; set; }
    public int? ExternalApiId { get; set; }
}

public class CreateTeamRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Code { get; set; }
    public string? LogoUrl { get; set; }
    public int? ExternalApiId { get; set; }
}

public class UpdateTeamRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Code { get; set; }
    public string? LogoUrl { get; set; }
}
