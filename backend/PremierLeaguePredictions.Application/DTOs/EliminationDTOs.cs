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

    /// <summary>Their points when they went out.</summary>
    public int TotalPoints { get; set; }
    public int PicksMade { get; set; }
    public decimal AveragePointsPerGame { get; set; }

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

    /// <summary>Points per game separating them from the first player out of the zone.</summary>
    public decimal AverageBehindSafety { get; set; }
}
