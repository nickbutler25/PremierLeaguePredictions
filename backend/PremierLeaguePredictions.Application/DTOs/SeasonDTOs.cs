namespace PremierLeaguePredictions.Application.DTOs;

public class SeasonDto
{
    public string Name { get; set; } = string.Empty; // Primary identifier
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateSeasonRequest
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? ExternalSeasonYear { get; set; } // For syncing fixtures from Football Data API
}

public class CreateSeasonResponse
{
    public string SeasonId { get; set; } = string.Empty; // Season Name
    public string Message { get; set; } = string.Empty;
    public int TeamsCreated { get; set; }
    public int TeamsActivated { get; set; }
    public int TeamsDeactivated { get; set; }
    public int GameweeksCreated { get; set; }
    public int FixturesCreated { get; set; }
}

public class SyncTeamsResponse
{
    public string Message { get; set; } = string.Empty;
    public int TeamsCreated { get; set; }
    public int TeamsUpdated { get; set; }
    public int TotalActiveTeams { get; set; }
}

public class SyncFixturesResponse
{
    public string Message { get; set; } = string.Empty;
    public int FixturesCreated { get; set; }
    public int FixturesUpdated { get; set; }
    public int GameweeksCreated { get; set; }
}

public class TeamStatusDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; }
}
