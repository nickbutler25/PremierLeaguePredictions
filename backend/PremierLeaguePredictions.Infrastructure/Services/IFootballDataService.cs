namespace PremierLeaguePredictions.Infrastructure.Services;

public interface IFootballDataService
{
    Task<IEnumerable<ExternalFixture>> GetFixturesAsync(int? season = null, CancellationToken cancellationToken = default);
    Task<ExternalFixture?> GetFixtureByIdAsync(int externalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every fixture in one matchday, in a single call.
    /// </summary>
    /// <remarks>
    /// Preferred over <see cref="GetFixtureByIdAsync"/> for anything live. Measured against a
    /// match in play on 2026-09-05, the per-match route served a snapshot up to 3m07s older
    /// than this one for the same fixture, with the same key, in the same second — the two are
    /// backed by different caches and the per-match one is the stale side. It is also ten times
    /// cheaper for a full gameweek and keeps the burst off the free tier's 10 calls/min.
    /// </remarks>
    Task<IEnumerable<ExternalFixture>> GetFixturesByMatchdayAsync(int matchday, CancellationToken cancellationToken = default);
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
    public string? ShortName { get; set; }
    public string? Tla { get; set; }
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
