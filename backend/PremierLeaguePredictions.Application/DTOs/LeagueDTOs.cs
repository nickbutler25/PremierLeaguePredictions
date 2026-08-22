namespace PremierLeaguePredictions.Application.DTOs;

public class LeagueStandingsDto
{
    public List<StandingEntryDto> Standings { get; set; } = new();
    public int TotalPlayers { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class StandingEntryDto
{
    public int Position { get; set; }
    public int Rank { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int TotalPoints { get; set; }
    public int PicksMade { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDifference { get; set; }
    public bool IsEliminated { get; set; }
    public int? EliminatedInGameweek { get; set; }
    public int? EliminationPosition { get; set; }

    /// <summary>
    /// This player's pick for the gameweek currently in progress. Null when no gameweek is
    /// active, or when the deadline has not passed yet — picks stay private until they lock.
    /// </summary>
    public PickSummaryDto? CurrentPick { get; set; }

    /// <summary>
    /// The player's picks from the last completed gameweeks, oldest first so the row reads
    /// left to right as form over time. A gameweek only counts once all of its fixtures are
    /// done, so a gameweek in progress never appears here even in part.
    /// </summary>
    public List<PickSummaryDto> Form { get; set; } = new();

    /// <summary>
    /// Shallow copy. The standings aggregate is cached, but CurrentPick must reflect live
    /// scores, so each request layers the pick onto its own copy rather than mutating the
    /// shared cached instance.
    /// </summary>
    public StandingEntryDto Copy() => (StandingEntryDto)MemberwiseClone();
}

/// <summary>
/// How a pick is faring. Serialised as a string so the client matches on names.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(
    typeof(System.Text.Json.Serialization.JsonStringEnumConverter<PickOutcome>))]
public enum PickOutcome
{
    /// <summary>Revealed, but the match has not kicked off yet.</summary>
    Pending,
    Win,
    Draw,
    Loss
}

/// <summary>
/// A pick, with enough detail to render a crest and colour it by how the match went. Used
/// both for the pick in progress and for entries in the form guide.
/// </summary>
public class PickSummaryDto
{
    public int GameweekNumber { get; set; }
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;

    /// <summary>Short form for narrow columns, e.g. "BRE".</summary>
    public string? TeamShortName { get; set; }

    public string? LogoUrl { get; set; }

    public PickOutcome Outcome { get; set; } = PickOutcome.Pending;

    /// <summary>
    /// True while the match is under way, so the outcome is provisional and may still change.
    /// </summary>
    public bool IsLive { get; set; }

    public string? OpponentName { get; set; }
    public int? TeamScore { get; set; }
    public int? OpponentScore { get; set; }
}
