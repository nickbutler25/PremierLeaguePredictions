namespace PremierLeaguePredictions.Application.DTOs;

public class GameweekDto
{
    public string SeasonId { get; set; } = string.Empty;
    public int WeekNumber { get; set; }
    public DateTime Deadline { get; set; }
    public bool IsLocked { get; set; }
    public int EliminationCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Status { get; set; } // "Upcoming", "InProgress", or null
}

public class CreateGameweekRequest
{
    public string SeasonId { get; set; } = string.Empty;
    public int WeekNumber { get; set; }
    public DateTime Deadline { get; set; }
}

public class UpdateGameweekRequest
{
    public DateTime? Deadline { get; set; }
    public bool? IsLocked { get; set; }
    public int? EliminationCount { get; set; }
}

public class GameweekWithFixturesDto : GameweekDto
{
    public List<FixtureDto> Fixtures { get; set; } = new();
}

public class ResultsSyncResponse
{
    public int FixturesUpdated { get; set; }
    public int GameweeksProcessed { get; set; }
    public int PicksRecalculated { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<FixtureUpdateDetail> UpdatedFixtures { get; set; } = new();
}

public class FixtureUpdateDetail
{
    public Guid FixtureId { get; set; }
    public int GameweekNumber { get; set; }
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
}
