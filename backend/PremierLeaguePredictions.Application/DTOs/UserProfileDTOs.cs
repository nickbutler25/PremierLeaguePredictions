namespace PremierLeaguePredictions.Application.DTOs;

/// <summary>
/// Another player's season, as everyone else is allowed to see it.
/// </summary>
/// <remarks>
/// Everything here is built from picks whose gameweek deadline has already passed. A pick for a
/// gameweek still open is private — seeing it would let the viewer pick around it — and that
/// applies to what can be *inferred* as much as to the pick itself, so an unrevealed pick is
/// left out of the team usage and the head-to-head too, not just the pick list.
/// </remarks>
public class UserProfileDto
{
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }

    public string SeasonId { get; set; } = string.Empty;

    /// <summary>Null when this player has no standings row for the season.</summary>
    public UserStandingDto? Standing { get; set; }

    /// <summary>
    /// The pick for the gameweek in progress, once its deadline has passed. Null before then,
    /// and null when no gameweek is under way.
    /// </summary>
    public PickSummaryDto? CurrentPick { get; set; }

    /// <summary>The gameweek CurrentPick belongs to, for labelling it.</summary>
    public int? CurrentGameweek { get; set; }

    /// <summary>Revealed picks from earlier gameweeks, most recent first.</summary>
    public List<PickSummaryDto> PreviousPicks { get; set; } = new();

    public TeamUsageDto? TeamUsage { get; set; }

    /// <summary>Null when a player is looking at their own profile.</summary>
    public HeadToHeadDto? HeadToHead { get; set; }
}

public class UserStandingDto
{
    public int Position { get; set; }
    public int TotalPoints { get; set; }
    public int PicksMade { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDifference { get; set; }

    /// <summary>
    /// Points per gameweek played, to two decimals. This is the figure eliminations run on, so
    /// it matters more to a player's survival than the raw total.
    /// </summary>
    public decimal AveragePointsPerGame { get; set; }

    public bool IsEliminated { get; set; }
    public int? EliminatedInGameweek { get; set; }
}

/// <summary>
/// Which teams the player has spent and which they have left for the current half. Built only
/// from revealed picks, so it never gives away a pick that has not locked yet.
/// </summary>
public class TeamUsageDto
{
    public int Half { get; set; }
    public int FirstGameweek { get; set; }
    public int LastGameweek { get; set; }

    /// <summary>How many times one team may be picked in this half, per the season's rules.</summary>
    public int MaxTimesTeamCanBePicked { get; set; }

    public List<TeamUsageEntryDto> Used { get; set; } = new();
    public List<TeamUsageEntryDto> Available { get; set; } = new();
}

public class TeamUsageEntryDto
{
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string? TeamShortName { get; set; }
    public string? LogoUrl { get; set; }

    /// <summary>Times picked so far this half. Zero for a team that is still available.</summary>
    public int TimesPicked { get; set; }
}

/// <summary>
/// The viewer's season against this player's, over the gameweeks both have a revealed pick for.
/// </summary>
public class HeadToHeadDto
{
    public int GameweeksCompared { get; set; }
    public int SamePickCount { get; set; }
    public int ViewerPoints { get; set; }
    public int PlayerPoints { get; set; }

    /// <summary>Gameweeks where the two picked differently, most recent first.</summary>
    public List<HeadToHeadGameweekDto> Differences { get; set; } = new();
}

public class HeadToHeadGameweekDto
{
    public int GameweekNumber { get; set; }
    public PickSummaryDto ViewerPick { get; set; } = null!;
    public PickSummaryDto PlayerPick { get; set; } = null!;
    public int ViewerPoints { get; set; }
    public int PlayerPoints { get; set; }
}
