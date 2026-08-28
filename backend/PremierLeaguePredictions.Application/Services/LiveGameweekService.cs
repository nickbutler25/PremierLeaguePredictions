using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Application.Services;

/// <summary>
/// Builds the gameweek page: the viewer's pick, what the rest of the field took, and who the
/// week moved them against — for the gameweek being played, or any earlier one.
/// </summary>
/// <remarks>
/// The table is rebuilt <em>as it stood at the end of the selected gameweek</em> rather than
/// read from the standings, because the standings only know about now. Showing today's
/// positions against a pick from twelve weeks ago would be a different question than the one
/// the page is asking. For the live gameweek the two coincide, and
/// <c>LiveGameweekServiceTests</c> pins that they agree — the standings remain the authority,
/// and this must not be allowed to drift from them.
/// </remarks>
public class LiveGameweekService : ILiveGameweekService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPickRuleService _pickRuleService;
    private readonly ILogger<LiveGameweekService> _logger;

    /// <summary>How many players either side of the viewer count as breathing down their neck.</summary>
    private const int RivalWindow = 3;

    /// <summary>How much of the top of the table to show.</summary>
    private const int LeaderCount = 3;

    /// <summary>
    /// Fixture statuses that cannot change again, matching <see cref="LeagueService"/>. A
    /// postponed fixture is deliberately not one of them: it keeps the gameweek open.
    /// </summary>
    private static readonly HashSet<string> SettledStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "FINISHED", "AWARDED", "CANCELLED" };

    /// <summary>Statuses that mean a score exists and is worth points, settled or not.</summary>
    private static readonly HashSet<string> ScoringStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "FINISHED", "IN_PLAY", "PAUSED" };

    public LiveGameweekService(
        IUnitOfWork unitOfWork,
        IPickRuleService pickRuleService,
        ILogger<LiveGameweekService> logger)
    {
        _unitOfWork = unitOfWork;
        _pickRuleService = pickRuleService;
        _logger = logger;
    }

    public async Task<LiveGameweekDto> GetGameweekAsync(
        Guid userId,
        string? seasonId = null,
        int? gameweekNumber = null,
        CancellationToken cancellationToken = default)
    {
        seasonId ??= (await _unitOfWork.Seasons.FindAsync(s => s.IsActive, trackChanges: false, cancellationToken))
            .FirstOrDefault()?.Name;

        var result = new LiveGameweekDto { SeasonId = seasonId ?? string.Empty };

        if (string.IsNullOrEmpty(seasonId))
        {
            result.ClosedReason = "no-gameweek";
            return result;
        }

        var now = DateTime.UtcNow;

        var gameweeks = (await _unitOfWork.Gameweeks.FindAsync(
                g => g.SeasonId == seasonId, trackChanges: false, cancellationToken))
            .OrderBy(g => g.WeekNumber)
            .ToList();

        var seasonFixtures = (await _unitOfWork.Fixtures.FindAsync(
                f => f.SeasonId == seasonId, trackChanges: false, cancellationToken))
            .ToList();

        var fixturesByGameweek = seasonFixtures
            .GroupBy(f => f.GameweekNumber)
            .ToDictionary(g => g.Key, g => g.ToList());

        var live = await ActiveGameweekFinder.FindAsync(_unitOfWork, seasonId, cancellationToken);

        // The whole archive rests on this line: a gameweek is selectable only once its deadline
        // has passed. One with no fixtures is skipped — there is nothing to show and it would
        // sit in the selector as a dead option.
        var revealed = gameweeks
            .Where(g => g.Deadline <= now && fixturesByGameweek.ContainsKey(g.WeekNumber))
            .ToList();

        result.AvailableGameweeks = revealed
            .OrderByDescending(g => g.WeekNumber)
            .Select(g => new GameweekOptionDto
            {
                GameweekNumber = g.WeekNumber,
                Deadline = g.Deadline,
                IsLive = live != null && live.WeekNumber == g.WeekNumber,
                IsComplete = IsComplete(fixturesByGameweek.GetValueOrDefault(g.WeekNumber))
            })
            .ToList();

        // Default to what is being played, and to the last one played when nothing is. Asking
        // for a gameweek that has not locked finds nothing here, which is the reveal gate doing
        // its job rather than an error to report.
        var selected = gameweekNumber.HasValue
            ? revealed.FirstOrDefault(g => g.WeekNumber == gameweekNumber.Value)
            : live ?? revealed.OrderByDescending(g => g.WeekNumber).FirstOrDefault();

        if (selected == null)
            return Closed(result, gameweeks, now);

        var fixtures = fixturesByGameweek.GetValueOrDefault(selected.WeekNumber) ?? new List<Fixture>();

        result.IsRevealed = true;
        result.IsLive = live != null && live.WeekNumber == selected.WeekNumber;
        result.IsComplete = IsComplete(fixtures);
        result.GameweekNumber = selected.WeekNumber;
        result.Deadline = selected.Deadline;

        var picks = (await _unitOfWork.Picks.FindAsync(
                p => p.SeasonId == seasonId && p.GameweekNumber <= selected.WeekNumber,
                trackChanges: false, cancellationToken))
            .ToList();

        var teams = (await _unitOfWork.Teams.FindAsync(t => true, trackChanges: false, cancellationToken))
            .ToDictionary(t => t.Id);

        var roster = await BuildRosterAsync(seasonId, cancellationToken);
        var eliminations = (await _unitOfWork.UserEliminations.FindAsync(
                e => e.SeasonId == seasonId, trackChanges: false, cancellationToken))
            .ToList();

        // Only approved players belong on the page. A pick from someone who has since lost
        // their place in the season would otherwise skew every percentage on it.
        var pickByUser = picks
            .Where(p => p.GameweekNumber == selected.WeekNumber && roster.ContainsKey(p.UserId))
            .ToDictionary(p => p.UserId);

        result.TotalPlayers = roster.Count;
        result.PlayersWithPick = pickByUser.Count;

        result.FixturesTotal = fixtures.Count;
        result.FixturesSettled = fixtures.Count(f => SettledStatuses.Contains(f.Status));
        result.FixturesInPlay = fixtures.Count(f => f.Status is "IN_PLAY" or "PAUSED");
        result.FixturesToKickOff = fixtures.Count(f =>
            !SettledStatuses.Contains(f.Status) && f.Status is not ("IN_PLAY" or "PAUSED"));

        pickByUser.TryGetValue(userId, out var myPick);

        // Built once and shared: the ownership list, the rival rows and the danger zone all
        // draw the same picks, and building them per section invites them to disagree.
        var summaryByUser = pickByUser.ToDictionary(
            entry => entry.Key,
            entry => PickSummaryFactory.Build(selected.WeekNumber, entry.Value.TeamId, fixtures, teams));

        // Eliminated as at the selected gameweek, not as at today: a player still in the running
        // back then must not be struck through in an archived week.
        var eliminatedByThen = eliminations
            .Where(e => e.GameweekNumber <= selected.WeekNumber)
            .Select(e => e.UserId)
            .ToHashSet();

        result.Ownership = BuildOwnership(
            pickByUser, summaryByUser, teams, roster, eliminatedByThen, userId, myPick);
        result.Fixtures = BuildFixtures(fixtures, pickByUser, teams, myPick);
        result.MyPick = BuildMyPick(myPick, summaryByUser, pickByUser, fixtures, userId);

        var scoring = ScoringPicks(seasonFixtures);
        var standings = Aggregate(roster, picks, selected.WeekNumber, scoring, eliminations);
        var before = Aggregate(roster, picks, selected.WeekNumber - 1, scoring, eliminations);

        result.Threat = BuildThreat(standings, before, summaryByUser, pickByUser, roster, userId, myPick);
        result.Threat.Danger = BuildDanger(
            selected, result.IsLive, standings, summaryByUser, roster, eliminations, userId);

        result.TeamUsage = await BuildTeamUsageAsync(
            seasonId, selected.WeekNumber, userId, picks, teams, cancellationToken);

        _logger.LogDebug(
            "Gameweek {SeasonId} GW{Week} for {UserId}: {Picks} picks across {Teams} teams (live: {IsLive})",
            seasonId, selected.WeekNumber, userId, result.PlayersWithPick, result.Ownership.Count, result.IsLive);

        return result;
    }

    private static bool IsComplete(List<Fixture>? fixtures) =>
        fixtures is { Count: > 0 } && fixtures.All(f => SettledStatuses.Contains(f.Status));

    /// <summary>
    /// Nothing to show. Only reachable before the season's first deadline, or when a season has
    /// no gameweeks at all — once one has been played it stays in the archive.
    /// </summary>
    private static LiveGameweekDto Closed(LiveGameweekDto result, List<Gameweek> gameweeks, DateTime now)
    {
        var next = gameweeks.Where(g => g.Deadline > now).OrderBy(g => g.Deadline).FirstOrDefault();

        result.IsRevealed = false;
        result.ClosedReason = next != null ? "before-deadline" : "no-gameweek";
        result.NextDeadline = next?.Deadline;
        result.GameweekNumber = next?.WeekNumber;
        return result;
    }

    /// <summary>The approved field, by id — the only players who appear anywhere on the page.</summary>
    private async Task<Dictionary<Guid, PlayerProfile>> BuildRosterAsync(
        string seasonId, CancellationToken cancellationToken)
    {
        var approved = (await _unitOfWork.SeasonParticipations.FindAsync(
                sp => sp.SeasonId == seasonId && sp.IsApproved, trackChanges: false, cancellationToken))
            .Select(sp => sp.UserId)
            .ToList();

        if (approved.Count == 0)
            return new Dictionary<Guid, PlayerProfile>();

        var users = await _unitOfWork.Users.FindAsync(
            u => approved.Contains(u.Id), trackChanges: false, cancellationToken);

        return users.ToDictionary(
            u => u.Id,
            u => new PlayerProfile($"{u.FirstName} {u.LastName}"));
    }

    private sealed record PlayerProfile(string Name);

    /// <summary>
    /// Which (gameweek, team) pairs have a score, and so count towards a player's record.
    /// </summary>
    private static HashSet<(int Gameweek, int TeamId)> ScoringPicks(List<Fixture> seasonFixtures) =>
        seasonFixtures
            .Where(f => ScoringStatuses.Contains(f.Status))
            .SelectMany(f => new[]
            {
                (f.GameweekNumber, f.HomeTeamId),
                (f.GameweekNumber, f.AwayTeamId)
            })
            .ToHashSet();

    /// <summary>
    /// The table as it stood at the end of <paramref name="cutoff"/>.
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="LeagueService"/> exactly: total points count every pick, while games
    /// played, results and goals count only picks whose fixture has a score — so a player yet
    /// to kick off is not scored as having lost. The sort is the standings' own chain, ending
    /// on the id, which is not a ranking but stops players level on every column from shuffling
    /// between requests.
    /// </remarks>
    private static List<StandingEntryDto> Aggregate(
        Dictionary<Guid, PlayerProfile> roster,
        List<Pick> picks,
        int cutoff,
        HashSet<(int Gameweek, int TeamId)> scoring,
        List<UserElimination> eliminations)
    {
        var picksByUser = picks
            .Where(p => p.GameweekNumber <= cutoff && roster.ContainsKey(p.UserId))
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var eliminationByUser = eliminations
            .Where(e => e.GameweekNumber <= cutoff)
            .GroupBy(e => e.UserId)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.GameweekNumber).First());

        var entries = roster
            .Select(player =>
            {
                var userPicks = picksByUser.GetValueOrDefault(player.Key) ?? new List<Pick>();
                var counted = userPicks
                    .Where(p => scoring.Contains((p.GameweekNumber, p.TeamId)))
                    .ToList();

                eliminationByUser.TryGetValue(player.Key, out var elimination);

                return new StandingEntryDto
                {
                    UserId = player.Key,
                    UserName = player.Value.Name,
                    TotalPoints = userPicks.Sum(p => p.Points),
                    PicksMade = counted.Count,
                    Wins = counted.Count(p => p.Points == 3),
                    Draws = counted.Count(p => p.Points == 1),
                    Losses = counted.Count(p => p.Points == 0),
                    GoalsFor = counted.Sum(p => p.GoalsFor),
                    GoalsAgainst = counted.Sum(p => p.GoalsAgainst),
                    GoalDifference = counted.Sum(p => p.GoalsFor) - counted.Sum(p => p.GoalsAgainst),
                    IsEliminated = elimination != null,
                    EliminatedInGameweek = elimination?.GameweekNumber,
                    EliminationPosition = elimination?.Position
                };
            })
            .OrderByDescending(e => e.TotalPoints)
            .ThenByDescending(e => e.AveragePointsPerGame)
            .ThenByDescending(e => e.GoalDifference)
            .ThenByDescending(e => e.GoalsFor)
            .ThenByDescending(e => e.UserId)
            .ToList();

        for (var i = 0; i < entries.Count; i++)
        {
            entries[i].Position = i + 1;
            entries[i].Rank = i + 1;
        }

        return entries;
    }

    private static List<TeamOwnershipDto> BuildOwnership(
        Dictionary<Guid, Pick> pickByUser,
        Dictionary<Guid, PickSummaryDto?> summaryByUser,
        Dictionary<int, Team> teams,
        Dictionary<Guid, PlayerProfile> roster,
        HashSet<Guid> eliminated,
        Guid userId,
        Pick? myPick)
    {
        var total = pickByUser.Count;
        if (total == 0)
            return new List<TeamOwnershipDto>();

        return pickByUser.Values
            .GroupBy(p => p.TeamId)
            .Select(group =>
            {
                var summary = summaryByUser.GetValueOrDefault(group.First().UserId);
                var team = teams.GetValueOrDefault(group.Key);

                return new TeamOwnershipDto
                {
                    TeamId = group.Key,
                    TeamName = summary?.TeamName ?? team?.Name ?? "Unknown",
                    TeamShortName = summary?.TeamShortName ?? team?.MediumName ?? team?.Code,
                    LogoUrl = summary?.LogoUrl ?? team?.LogoUrl,
                    Count = group.Count(),
                    Percent = Percent(group.Count(), total),
                    IsMyPick = myPick != null && myPick.TeamId == group.Key,
                    OpponentName = summary?.OpponentName,
                    TeamScore = summary?.TeamScore,
                    OpponentScore = summary?.OpponentScore,
                    Outcome = summary?.Outcome ?? PickOutcome.Pending,
                    IsLive = summary?.IsLive ?? false,
                    // Every pick on a team scores the same, so one of them speaks for the group.
                    Points = group.First().Points,
                    Owners = group
                        .Select(p => new PickOwnerDto
                        {
                            UserId = p.UserId,
                            UserName = roster.GetValueOrDefault(p.UserId)?.Name ?? "Unknown",
                            IsMe = p.UserId == userId,
                            IsEliminated = eliminated.Contains(p.UserId)
                        })
                        // The viewer first, then alphabetically, so a long list opens on
                        // something the reader is looking for.
                        .OrderByDescending(o => o.IsMe)
                        .ThenBy(o => o.UserName, StringComparer.CurrentCultureIgnoreCase)
                        .ToList()
                };
            })
            .OrderByDescending(o => o.Count)
            .ThenBy(o => o.TeamName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static List<LiveFixtureDto> BuildFixtures(
        List<Fixture> fixtures,
        Dictionary<Guid, Pick> pickByUser,
        Dictionary<int, Team> teams,
        Pick? myPick)
    {
        var picksByTeam = pickByUser.Values
            .GroupBy(p => p.TeamId)
            .ToDictionary(g => g.Key, g => g.Count());

        return fixtures
            .Select(f =>
            {
                var home = teams.GetValueOrDefault(f.HomeTeamId);
                var away = teams.GetValueOrDefault(f.AwayTeamId);

                return new LiveFixtureDto
                {
                    Id = f.Id,
                    KickoffTime = f.KickoffTime,
                    Status = f.Status,
                    HomeTeamId = f.HomeTeamId,
                    HomeTeamName = home?.Name ?? "Unknown",
                    HomeTeamShortName = home?.MediumName ?? home?.Code,
                    HomeTeamLogoUrl = home?.LogoUrl,
                    AwayTeamId = f.AwayTeamId,
                    AwayTeamName = away?.Name ?? "Unknown",
                    AwayTeamShortName = away?.MediumName ?? away?.Code,
                    AwayTeamLogoUrl = away?.LogoUrl,
                    HomeScore = f.HomeScore,
                    AwayScore = f.AwayScore,
                    HomePickCount = picksByTeam.GetValueOrDefault(f.HomeTeamId),
                    AwayPickCount = picksByTeam.GetValueOrDefault(f.AwayTeamId),
                    HasMyPick = myPick != null &&
                                (f.HomeTeamId == myPick.TeamId || f.AwayTeamId == myPick.TeamId),
                    IsLive = f.Status is "IN_PLAY" or "PAUSED",
                    IsSettled = SettledStatuses.Contains(f.Status)
                };
            })
            // The viewer's own match first — it is the only one they are watching — then in
            // kickoff order so the rest reads as the weekend runs.
            .OrderByDescending(f => f.HasMyPick)
            .ThenBy(f => f.KickoffTime)
            .ToList();
    }

    private static MyLivePickDto? BuildMyPick(
        Pick? myPick,
        Dictionary<Guid, PickSummaryDto?> summaryByUser,
        Dictionary<Guid, Pick> pickByUser,
        List<Fixture> fixtures,
        Guid userId)
    {
        if (myPick == null)
            return null;

        var summary = summaryByUser.GetValueOrDefault(userId);
        if (summary == null)
            return null;

        var others = pickByUser.Values.Where(p => p.UserId != userId).ToList();
        var sameTeam = pickByUser.Values.Count(p => p.TeamId == myPick.TeamId);
        var myFixture = fixtures.FirstOrDefault(f =>
            f.HomeTeamId == myPick.TeamId || f.AwayTeamId == myPick.TeamId);

        return new MyLivePickDto
        {
            Pick = summary,
            IsAutoAssigned = myPick.IsAutoAssigned,
            KickoffTime = myFixture?.KickoffTime,
            OwnedByCount = sameTeam,
            OwnershipPercent = Percent(sameTeam, pickByUser.Count),
            PointsSoFar = myPick.Points,
            PlayersGainedOn = others.Count(p => p.Points < myPick.Points),
            PlayersLevelWith = others.Count(p => p.Points == myPick.Points),
            PlayersLostTo = others.Count(p => p.Points > myPick.Points),
            FieldAveragePoints = pickByUser.Count == 0
                ? 0m
                : Math.Round((decimal)pickByUser.Values.Sum(p => p.Points) / pickByUser.Count, 2)
        };
    }

    private static LiveThreatDto BuildThreat(
        List<StandingEntryDto> standings,
        List<StandingEntryDto> before,
        Dictionary<Guid, PickSummaryDto?> summaryByUser,
        Dictionary<Guid, Pick> pickByUser,
        Dictionary<Guid, PlayerProfile> roster,
        Guid userId,
        Pick? myPick)
    {
        var threat = new LiveThreatDto();
        if (standings.Count == 0)
            return threat;

        var positionBefore = before.ToDictionary(e => e.UserId, e => e.Position);
        var myEntry = standings.FirstOrDefault(e => e.UserId == userId);

        LiveRivalDto ToRival(StandingEntryDto entry)
        {
            pickByUser.TryGetValue(entry.UserId, out var theirPick);
            return new LiveRivalDto
            {
                UserId = entry.UserId,
                UserName = entry.UserName,
                Position = entry.Position,
                PositionChange = positionBefore.TryGetValue(entry.UserId, out var was)
                    ? was - entry.Position
                    : 0,
                TotalPoints = entry.TotalPoints,
                PointsFromMe = myEntry == null ? 0 : entry.TotalPoints - myEntry.TotalPoints,
                IsMe = entry.UserId == userId,
                IsEliminated = entry.IsEliminated,
                SharesMyPick = myPick != null && theirPick != null && theirPick.TeamId == myPick.TeamId,
                Pick = summaryByUser.GetValueOrDefault(entry.UserId)
            };
        }

        threat.MyPosition = myEntry?.Position;
        threat.MyPositionBefore = myEntry != null && positionBefore.TryGetValue(userId, out var myBefore)
            ? myBefore
            : null;

        if (myEntry != null)
        {
            var myIndex = standings.IndexOf(myEntry);
            var from = Math.Max(0, myIndex - RivalWindow);
            var to = Math.Min(standings.Count - 1, myIndex + RivalWindow);

            // The viewer's own row is kept in the slice: the window is a piece of the table,
            // and cutting the reader out of it makes the rows above and below hard to place.
            threat.Rivals = standings.Skip(from).Take(to - from + 1).Select(ToRival).ToList();
        }

        threat.Leaders = standings.Take(LeaderCount).Select(ToRival).ToList();

        if (myPick != null)
        {
            threat.SharingMyPick = pickByUser.Values.Count(p => p.UserId != userId && p.TeamId == myPick.TeamId);
            threat.OnDifferentPick = pickByUser.Values.Count(p => p.UserId != userId && p.TeamId != myPick.TeamId);
        }

        return threat;
    }

    /// <summary>
    /// The elimination attached to this gameweek: what actually happened where it has been run,
    /// and what would happen if the scores stood where it has not.
    /// </summary>
    private static LiveDangerDto? BuildDanger(
        Gameweek gameweek,
        bool isLive,
        List<StandingEntryDto> standings,
        Dictionary<Guid, PickSummaryDto?> summaryByUser,
        Dictionary<Guid, PlayerProfile> roster,
        List<UserElimination> eliminations,
        Guid userId)
    {
        var settled = eliminations
            .Where(e => e.GameweekNumber == gameweek.WeekNumber)
            .OrderBy(e => e.Position)
            .ToList();

        if (settled.Count > 0)
        {
            var recordByUser = standings.ToDictionary(e => e.UserId);

            return new LiveDangerDto
            {
                IsSettled = true,
                EliminationCount = settled.Count,
                AmIInTheZone = settled.Any(e => e.UserId == userId),
                Players = settled
                    .Select(e =>
                    {
                        var record = recordByUser.GetValueOrDefault(e.UserId);
                        return new LiveAtRiskDto
                        {
                            UserId = e.UserId,
                            UserName = roster.GetValueOrDefault(e.UserId)?.Name ?? "Unknown",
                            IsMe = e.UserId == userId,
                            AveragePointsPerGame = record == null
                                ? 0m
                                : Average(record.TotalPoints, record.PicksMade),
                            Pick = summaryByUser.GetValueOrDefault(e.UserId)
                        };
                    })
                    .ToList()
            };
        }

        // Only a gameweek still being played gets a forecast. One that finished without its
        // eliminations being run has no answer to give — the admin has not run it yet, and
        // guessing would put names in a drop zone that may never be applied.
        if (!isLive || gameweek.EliminationCount <= 0)
            return null;

        // The same chain the elimination run and the eliminations page order by, so a player
        // cannot be shown as safe here and then taken by the run.
        var contenders = standings
            .Where(e => !e.IsEliminated)
            .Select(e => new { Entry = e, Average = Average(e.TotalPoints, e.PicksMade) })
            .OrderBy(x => x.Entry.TotalPoints)
            .ThenBy(x => x.Average)
            .ThenBy(x => x.Entry.GoalDifference)
            .ThenBy(x => x.Entry.GoalsFor)
            .ThenBy(x => x.Entry.UserId)
            .ToList();

        // The first player outside the zone sets the bar the rest have to clear.
        var safetyAverage = contenders.Count > gameweek.EliminationCount
            ? contenders[gameweek.EliminationCount].Average
            : (decimal?)null;

        var danger = new LiveDangerDto
        {
            IsSettled = false,
            EliminationCount = gameweek.EliminationCount,
            Players = contenders
                .Take(gameweek.EliminationCount)
                .Select(x => new LiveAtRiskDto
                {
                    UserId = x.Entry.UserId,
                    UserName = x.Entry.UserName,
                    IsMe = x.Entry.UserId == userId,
                    AveragePointsPerGame = x.Average,
                    AverageBehindSafety = safetyAverage.HasValue
                        ? Math.Round(safetyAverage.Value - x.Average, 2)
                        : 0m,
                    Pick = summaryByUser.GetValueOrDefault(x.Entry.UserId)
                })
                .ToList()
        };

        var myIndex = contenders.FindIndex(x => x.Entry.UserId == userId);
        if (myIndex >= 0)
        {
            danger.AmIInTheZone = myIndex < gameweek.EliminationCount;
            danger.MyMarginToSafety = safetyAverage.HasValue
                ? Math.Round(safetyAverage.Value - contenders[myIndex].Average, 2)
                : null;
        }

        return danger;
    }

    /// <summary>
    /// The viewer's team usage as it stood after this gameweek — safe to count, because every
    /// pick in it belongs to a gameweek whose deadline has passed.
    /// </summary>
    private async Task<TeamUsageDto> BuildTeamUsageAsync(
        string seasonId,
        int gameweekNumber,
        Guid userId,
        List<Pick> picks,
        Dictionary<int, Team> teams,
        CancellationToken cancellationToken)
    {
        var myPicks = picks
            .Where(p => p.UserId == userId && p.GameweekNumber <= gameweekNumber)
            .ToList();

        var rules = await _pickRuleService.GetPickRulesForSeasonAsync(seasonId);
        return TeamUsageBuilder.Build(myPicks, teams, rules, gameweekNumber);
    }

    private static decimal Average(int totalPoints, int picksMade) =>
        picksMade == 0 ? 0m : Math.Round((decimal)totalPoints / picksMade, 2);

    private static decimal Percent(int count, int total) =>
        total == 0 ? 0m : Math.Round((decimal)count * 100 / total, 1);
}
