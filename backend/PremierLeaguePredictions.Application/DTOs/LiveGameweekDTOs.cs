namespace PremierLeaguePredictions.Application.DTOs;

/// <summary>
/// One gameweek, as a single player sees it — live, or any earlier one.
/// </summary>
/// <remarks>
/// A gameweek is only ever served once its deadline has passed. Before that the payload is
/// empty apart from the countdown, because it reveals the entire field's picks and serving it
/// early would let a player pick around everyone else — the same reason the reveal gate sits on
/// <c>PickService</c>. History is safe under that rule and live is the interesting case, so the
/// two share one shape: <see cref="IsRevealed"/> says whether there is content, and
/// <see cref="IsLive"/> whether it is still changing.
/// </remarks>
public class LiveGameweekDto
{
    public string SeasonId { get; set; } = string.Empty;

    /// <summary>True when the selected gameweek is locked and still being played.</summary>
    public bool IsLive { get; set; }

    /// <summary>
    /// True when a gameweek has been selected and its deadline has passed, so the rest of the
    /// payload is populated. False means nothing may be shown yet.
    /// </summary>
    public bool IsRevealed { get; set; }

    /// <summary>True once every fixture in the selected gameweek has settled.</summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Why there is nothing to show, when there isn't: "before-deadline" while the first picks
    /// are still open, "no-gameweek" out of season. Null whenever <see cref="IsRevealed"/>.
    /// </summary>
    public string? ClosedReason { get; set; }

    /// <summary>The next deadline, so a closed page can count down to opening. Null if none.</summary>
    public DateTime? NextDeadline { get; set; }

    /// <summary>
    /// Every gameweek the viewer may look at — those whose deadline has passed — most recent
    /// first. Populated even when nothing is revealed, so the selector always has its options.
    /// </summary>
    public List<GameweekOptionDto> AvailableGameweeks { get; set; } = new();

    /// <summary>The gameweek this payload describes. Null when nothing is revealed.</summary>
    public int? GameweekNumber { get; set; }

    public DateTime? Deadline { get; set; }

    public int FixturesTotal { get; set; }
    public int FixturesSettled { get; set; }
    public int FixturesInPlay { get; set; }
    public int FixturesToKickOff { get; set; }

    /// <summary>Approved players holding a pick this gameweek, out of the full field.</summary>
    public int PlayersWithPick { get; set; }
    public int TotalPlayers { get; set; }

    /// <summary>Null for a player who made no pick.</summary>
    public MyLivePickDto? MyPick { get; set; }

    /// <summary>Every picked team by share of the field, most picked first.</summary>
    public List<TeamOwnershipDto> Ownership { get; set; } = new();

    /// <summary>The gameweek's fixtures, the viewer's own pick first, then by kickoff.</summary>
    public List<LiveFixtureDto> Fixtures { get; set; } = new();

    public LiveThreatDto Threat { get; set; } = new();

    /// <summary>What this gameweek's pick leaves the viewer for the rest of the half.</summary>
    public TeamUsageDto? TeamUsage { get; set; }
}

/// <summary>One entry in the gameweek selector.</summary>
public class GameweekOptionDto
{
    public int GameweekNumber { get; set; }
    public DateTime Deadline { get; set; }
    public bool IsLive { get; set; }
    public bool IsComplete { get; set; }
}

/// <summary>
/// The viewer's own pick, and what it is currently worth against everyone else's.
/// </summary>
public class MyLivePickDto
{
    public PickSummaryDto Pick { get; set; } = null!;

    /// <summary>True when the pick was assigned by the auto-pick job rather than chosen.</summary>
    public bool IsAutoAssigned { get; set; }

    public DateTime? KickoffTime { get; set; }

    /// <summary>How many players share this pick, the viewer included.</summary>
    public int OwnedByCount { get; set; }

    /// <summary>That count as a share of players holding any pick, to one decimal.</summary>
    public decimal OwnershipPercent { get; set; }

    /// <summary>Points the pick is currently earning — provisional while the match is live.</summary>
    public int PointsSoFar { get; set; }

    /// <summary>
    /// Players whose pick is currently earning less than the viewer's. This is what a
    /// differential is worth: a lightly-picked winner gains ground on nearly the whole field,
    /// while a heavily-picked one gains on almost nobody.
    /// </summary>
    public int PlayersGainedOn { get; set; }

    /// <summary>Players earning the same as the viewer, whether or not they picked the same team.</summary>
    public int PlayersLevelWith { get; set; }

    public int PlayersLostTo { get; set; }

    /// <summary>The field's mean points this gameweek, to two decimals.</summary>
    public decimal FieldAveragePoints { get; set; }
}

/// <summary>
/// One picked team and who took it. Only teams somebody picked appear.
/// </summary>
public class TeamOwnershipDto
{
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string? TeamShortName { get; set; }
    public string? LogoUrl { get; set; }

    public int Count { get; set; }

    /// <summary>Share of players holding a pick, to one decimal.</summary>
    public decimal Percent { get; set; }

    public bool IsMyPick { get; set; }

    public string? OpponentName { get; set; }
    public int? TeamScore { get; set; }
    public int? OpponentScore { get; set; }
    public PickOutcome Outcome { get; set; } = PickOutcome.Pending;
    public bool IsLive { get; set; }

    /// <summary>Points this pick is currently earning.</summary>
    public int Points { get; set; }

    /// <summary>
    /// Who picked it. Safe to send: the deadline has passed, so these picks are already public
    /// through the standings and each player's profile.
    /// </summary>
    public List<PickOwnerDto> Owners { get; set; } = new();
}

public class PickOwnerDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public bool IsMe { get; set; }
    public bool IsEliminated { get; set; }
}

public class LiveFixtureDto
{
    public Guid Id { get; set; }
    public DateTime KickoffTime { get; set; }
    public string Status { get; set; } = "SCHEDULED";

    public int HomeTeamId { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;
    public string? HomeTeamShortName { get; set; }
    public string? HomeTeamLogoUrl { get; set; }

    public int AwayTeamId { get; set; }
    public string AwayTeamName { get; set; } = string.Empty;
    public string? AwayTeamShortName { get; set; }
    public string? AwayTeamLogoUrl { get; set; }

    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }

    /// <summary>How many players this fixture carries on each side.</summary>
    public int HomePickCount { get; set; }
    public int AwayPickCount { get; set; }

    /// <summary>True when the viewer's pick is playing in this fixture.</summary>
    public bool HasMyPick { get; set; }

    public bool IsLive { get; set; }
    public bool IsSettled { get; set; }
}

/// <summary>
/// Everything bearing down on the viewer this gameweek: who is closing on them in the table,
/// who is running away with it, and whether the week is pushing them out of the competition.
/// </summary>
public class LiveThreatDto
{
    /// <summary>The viewer's live position, counting this gameweek's provisional points.</summary>
    public int? MyPosition { get; set; }

    /// <summary>
    /// Position before this gameweek's points, so the page can show movement. Null when the
    /// viewer has no standings row.
    /// </summary>
    public int? MyPositionBefore { get; set; }

    /// <summary>The players immediately above and below the viewer, closest first.</summary>
    public List<LiveRivalDto> Rivals { get; set; } = new();

    /// <summary>The top of the table, whether or not the viewer is near it.</summary>
    public List<LiveRivalDto> Leaders { get; set; } = new();

    /// <summary>Null when no elimination is configured for this gameweek.</summary>
    public LiveDangerDto? Danger { get; set; }

    /// <summary>
    /// Players holding the same pick as the viewer, excluding the viewer. The result cannot
    /// move the viewer relative to any of them, which is what makes the rest the real threat.
    /// </summary>
    public int SharingMyPick { get; set; }

    /// <summary>Players on a different pick, who this week's result does move them against.</summary>
    public int OnDifferentPick { get; set; }
}

public class LiveRivalDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;

    public int Position { get; set; }

    /// <summary>Change in position across this gameweek so far. Positive is a climb.</summary>
    public int PositionChange { get; set; }

    public int TotalPoints { get; set; }

    /// <summary>Points between this player and the viewer. Positive means they are ahead.</summary>
    public int PointsFromMe { get; set; }

    public bool IsMe { get; set; }
    public bool IsEliminated { get; set; }

    /// <summary>True when they picked the same team as the viewer, so this week cannot separate them.</summary>
    public bool SharesMyPick { get; set; }

    /// <summary>Their pick, once revealed. Null if they made none.</summary>
    public PickSummaryDto? Pick { get; set; }
}

/// <summary>
/// The elimination attached to this gameweek — a forecast while it is being played, and the
/// recorded outcome once it has been run.
/// </summary>
/// <remarks>
/// The forecast is ranked on the same chain as the elimination run and the eliminations page —
/// average points per game, then total, then games played, then goal difference — so a player
/// cannot be shown as safe here and then taken by the run.
/// </remarks>
public class LiveDangerDto
{
    /// <summary>
    /// True when this is what actually happened rather than what would happen: the gameweek's
    /// eliminations have been processed and these players are out.
    /// </summary>
    public bool IsSettled { get; set; }

    public int EliminationCount { get; set; }

    /// <summary>True when the viewer is currently filling one of those places.</summary>
    public bool AmIInTheZone { get; set; }

    /// <summary>
    /// Points per game between the viewer and safety. Positive means they are that far below
    /// the bar and have it to make up; negative is the cushion they hold above it.
    /// </summary>
    public decimal? MyMarginToSafety { get; set; }

    /// <summary>The players currently going out, worst first.</summary>
    public List<LiveAtRiskDto> Players { get; set; } = new();
}

public class LiveAtRiskDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public bool IsMe { get; set; }
    public decimal AveragePointsPerGame { get; set; }
    public decimal AverageBehindSafety { get; set; }
    public PickSummaryDto? Pick { get; set; }
}
