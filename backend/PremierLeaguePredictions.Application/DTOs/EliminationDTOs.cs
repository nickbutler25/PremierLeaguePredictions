namespace PremierLeaguePredictions.Application.DTOs;

public class UserEliminationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string SeasonId { get; set; } = string.Empty;
    public int GameweekNumber { get; set; }
    public int Position { get; set; }
    public int TotalPoints { get; set; }
    public DateTime EliminatedAt { get; set; }
    public Guid? EliminatedBy { get; set; }
    public string? EliminatedByName { get; set; }
}

public class ProcessEliminationsRequest
{
    public string SeasonId { get; set; } = string.Empty;
    public int GameweekNumber { get; set; }
}

public class ProcessEliminationsResponse
{
    public int PlayersEliminated { get; set; }
    public List<UserEliminationDto> EliminatedPlayers { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

public class UpdateGameweekEliminationRequest
{
    public string SeasonId { get; set; } = string.Empty;
    public int GameweekNumber { get; set; }
    public int EliminationCount { get; set; }
}

public class EliminationConfigDto
{
    public string GameweekId { get; set; } = string.Empty; // Format: "{SeasonId}-{WeekNumber}"
    public string SeasonId { get; set; } = string.Empty;
    public int WeekNumber { get; set; }
    public int EliminationCount { get; set; }
    public bool HasBeenProcessed { get; set; }
    public DateTime Deadline { get; set; }
}

/// <summary>
/// The eliminations page: who has gone out and when, and who the next gameweek threatens.
/// </summary>
/// <remarks>
/// Player-facing, so it carries none of the admin trail on <see cref="UserEliminationDto"/> —
/// which admin ran the process is not the players' business.
/// </remarks>
public class EliminationsOverviewDto
{
    public string SeasonId { get; set; } = string.Empty;

    /// <summary>Most recent gameweek first, so the latest casualties lead.</summary>
    public List<EliminatedPlayerDto> Eliminated { get; set; } = new();

    public int ActivePlayers { get; set; }
    public int TotalPlayers { get; set; }

    /// <summary>Null when no elimination is configured for the next gameweek.</summary>
    public DangerZoneDto? DangerZone { get; set; }
}

public class EliminatedPlayerDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }

    /// <summary>The gameweek after which they went out.</summary>
    public int GameweekNumber { get; set; }

    /// <summary>
    /// Where they finished: their position in the table at the moment they were eliminated.
    /// </summary>
    /// <remarks>
    /// Read from the elimination record rather than recomputed. The standings only know about
    /// now, and an eliminated player is not in them at all — so their place has to come from
    /// what was written down when the run took them.
    /// </remarks>
    public int FinalPosition { get; set; }

    /// <summary>Their points when they went out.</summary>
    public int TotalPoints { get; set; }
    public int PicksMade { get; set; }
    public decimal AveragePointsPerGame { get; set; }

    // The same record the full league table shows, so a player's season reads the same on both.
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDifference { get; set; }

    public DateTime EliminatedAt { get; set; }
}

/// <summary>
/// Who the next elimination would take if the season stopped now.
/// </summary>
public class DangerZoneDto
{
    public int GameweekNumber { get; set; }
    public DateTime Deadline { get; set; }

    /// <summary>How many go out after this gameweek.</summary>
    public int EliminationCount { get; set; }

    /// <summary>True once the gameweek's deadline has passed, so picks can no longer change it.</summary>
    public bool DeadlinePassed { get; set; }

    /// <summary>The players currently filling those places, worst first.</summary>
    public List<AtRiskPlayerDto> Players { get; set; } = new();

    /// <summary>
    /// Players who are safe but close enough to be caught: within
    /// <see cref="PointsFromDangerThreshold"/> of the best player in the zone. Worst first, so
    /// the one nearest the line comes first.
    /// </summary>
    /// <remarks>
    /// Measured against the top of the zone rather than the bottom of the table, because that is
    /// the player who would overtake them first — the gap that actually decides whether they
    /// stay up.
    /// </remarks>
    public List<AtRiskPlayerDto> JustSafe { get; set; } = new();

    /// <summary>How close to the zone a safe player has to be to appear in <see cref="JustSafe"/>.</summary>
    public int PointsFromDangerThreshold { get; set; }
}

public class AtRiskPlayerDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public int Position { get; set; }
    public int TotalPoints { get; set; }
    public int PicksMade { get; set; }
    public decimal AveragePointsPerGame { get; set; }

    // The same record the eliminated list and the full table carry, so the two halves of the
    // eliminations page read identically rather than each showing its own selection.
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDifference { get; set; }

    /// <summary>Points separating them from the first player out of the zone.</summary>
    /// <remarks>
    /// In points, matching what the page shows. <see cref="AverageBehindSafety"/> is the same
    /// gap in points per game and is what the gameweek page's threat card uses; both are kept
    /// because a column reading "0.33 behind" beside a column of whole points invites the wrong
    /// reading.
    /// </remarks>
    public int PointsBehindSafety { get; set; }

    /// <summary>
    /// For a player in <see cref="DangerZoneDto.JustSafe"/>, how many points clear of the zone
    /// they are. Zero for a player already in it.
    /// </summary>
    public int PointsClearOfZone { get; set; }

    /// <summary>Points per game separating them from the first player out of the zone.</summary>
    public decimal AverageBehindSafety { get; set; }
}
