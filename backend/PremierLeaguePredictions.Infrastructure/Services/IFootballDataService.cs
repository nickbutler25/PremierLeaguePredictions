namespace PremierLeaguePredictions.Infrastructure.Services;

public interface IFootballDataService
{
    Task<IEnumerable<ExternalFixture>> GetFixturesAsync(int? season = null, CancellationToken cancellationToken = default);
    Task<ExternalFixture?> GetFixtureByIdAsync(int externalId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ExternalTeam>> GetTeamsAsync(CancellationToken cancellationToken = default);
    Task<int> GetCurrentSeasonAsync(CancellationToken cancellationToken = default);
}

public class ExternalFixture
{
    public int Id { get; set; }
    public DateTime UtcDate { get; set; }
    public string Status { get; set; } = string.Empty; // SCHEDULED, TIMED, IN_PLAY, PAUSED, FINISHED, SUSPENDED, POSTPONED, CANCELLED
    public int? Matchday { get; set; }
    public ExternalTeamReference HomeTeam { get; set; } = null!;
    public ExternalTeamReference AwayTeam { get; set; } = null!;
    public ExternalScore? Score { get; set; }
}

public class ExternalTeamReference
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Crest { get; set; }
}

public class ExternalScore
{
    public ExternalScoreDetail? FullTime { get; set; }
    public ExternalScoreDetail? HalfTime { get; set; }
}

public class ExternalScoreDetail
{
    public int? Home { get; set; }
    public int? Away { get; set; }
}

public class ExternalTeam
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string? Crest { get; set; }
}

public class ExternalCompetition
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public ExternalSeason? CurrentSeason { get; set; }
}

public class ExternalSeason
{
    public int Id { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? CurrentMatchday { get; set; }
}
