using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Application.Services;

public class LeagueService : ILeagueService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LeagueService> _logger;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan StandingsCacheDuration = TimeSpan.FromMinutes(5);

    /// <summary>How many completed gameweeks the form guide shows.</summary>
    private const int FormLength = 10;

    /// <summary>
    /// Fixture statuses that cannot change again. A gameweek counts as complete once every one
    /// of its fixtures has reached one of these — a postponed match keeps it open, which is
    /// what it means for the gameweek not to have finished.
    /// </summary>
    private static readonly HashSet<string> SettledStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "FINISHED", "AWARDED", "CANCELLED" };

    public LeagueService(IUnitOfWork unitOfWork, ILogger<LeagueService> logger, IMemoryCache cache)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _cache = cache;
    }

    public void InvalidateStandings(string seasonId)
    {
        if (string.IsNullOrEmpty(seasonId))
            return;

        _cache.Remove(CacheKey(seasonId));
        _logger.LogDebug("Invalidated cached standings for season {SeasonId}", seasonId);
    }

    private static string CacheKey(string seasonId) => $"standings_{seasonId}";

    public async Task<LeagueStandingsDto> GetLeagueStandingsAsync(string? seasonId = null, CancellationToken cancellationToken = default)
    {
        // Get active season if not specified
        Season? activeSeason = null;
        if (string.IsNullOrEmpty(seasonId))
        {
            var seasons = await _unitOfWork.Seasons.FindAsync(s => s.IsActive, trackChanges: false, cancellationToken);
            activeSeason = seasons.FirstOrDefault();
            seasonId = activeSeason?.Name;
        }
        else
        {
            activeSeason = await _unitOfWork.Seasons.FirstOrDefaultAsync(s => s.Name == seasonId, trackChanges: false, cancellationToken);
        }

        if (string.IsNullOrEmpty(seasonId))
        {
            // No season found, return empty standings
            return new LeagueStandingsDto
            {
                Standings = new List<StandingEntryDto>(),
                TotalPlayers = 0,
                LastUpdated = DateTime.UtcNow
            };
        }

        // Check cache first. Only the aggregate is cached — current picks are attached after,
        // because they carry live scores and must not be five minutes stale.
        var cacheKey = CacheKey(seasonId);
        if (_cache.TryGetValue(cacheKey, out LeagueStandingsDto? cachedStandings) && cachedStandings != null)
        {
            _logger.LogDebug("Returning standings from cache for season {SeasonId}", seasonId);
            return await WithCurrentPicksAsync(cachedStandings, seasonId, cancellationToken);
        }

        _logger.LogInformation("Calculating standings for season {SeasonId}", seasonId);

        // Get approved participant IDs for filtering
        var approvedParticipations = await _unitOfWork.SeasonParticipations.FindAsync(
            sp => sp.SeasonId == seasonId && sp.IsApproved,
            trackChanges: false,
            cancellationToken);
        var approvedUserIds = approvedParticipations.Select(sp => sp.UserId).ToList();

        _logger.LogInformation("Found {Count} approved participants for season {SeasonId}", approvedUserIds.Count, seasonId);

        if (!approvedUserIds.Any())
        {
            return new LeagueStandingsDto
            {
                Standings = new List<StandingEntryDto>(),
                TotalPlayers = 0,
                LastUpdated = DateTime.UtcNow
            };
        }

        // Optimized query: Push all aggregation to the database via UnitOfWork
        var standingsData = await _unitOfWork.GetStandingsDataAsync(seasonId, approvedUserIds, cancellationToken);

        // Map to DTOs and calculate goal difference
        var standings = standingsData.Select(data => new StandingEntryDto
        {
            UserId = data.UserId,
            UserName = $"{data.FirstName} {data.LastName}",
            TotalPoints = data.TotalPoints,
            PicksMade = data.CompletedPicksCount,
            Wins = data.Wins,
            Draws = data.Draws,
            Losses = data.Losses,
            GoalsFor = data.GoalsFor,
            GoalsAgainst = data.GoalsAgainst,
            GoalDifference = data.GoalsFor - data.GoalsAgainst,
            IsEliminated = data.IsEliminated,
            EliminatedInGameweek = data.EliminationGameweek,
            EliminationPosition = data.EliminationPosition,
            Position = 0, // Will be calculated after sorting
            Rank = 0 // Will be calculated after sorting
        }).ToList();

        // Sort in memory (minimal data already loaded from database)
        // The id is not a ranking, but without it players level on every column come back in
        // whatever order the query happened to produce — so positions shuffled between requests,
        // and whoever landed last looked bottom of the table without being bottom of anything.
        // It mirrors the elimination ordering, so the table and the danger zone agree on ties.
        var sortedStandings = standings
            .OrderByDescending(s => s.TotalPoints)
            .ThenByDescending(s => s.GoalDifference)
            .ThenByDescending(s => s.GoalsFor)
            .ThenBy(s => s.PicksMade)
            .ThenByDescending(s => s.UserId)
            .ToList();

        // Assign positions and ranks
        for (int i = 0; i < sortedStandings.Count; i++)
        {
            sortedStandings[i].Position = i + 1;
            sortedStandings[i].Rank = i + 1;
        }

        _logger.LogInformation("Calculated standings for {Count} players", sortedStandings.Count);

        var result = new LeagueStandingsDto
        {
            Standings = sortedStandings,
            TotalPlayers = sortedStandings.Count,
            LastUpdated = DateTime.UtcNow
        };

        // Form goes in before the cache: it only changes when a match finishes, so five
        // minutes of staleness is harmless, and recomputing it per request would mean reading
        // every pick in the season on every page load.
        await AttachFormAsync(sortedStandings, seasonId, cancellationToken);

        // Cache the result
        _cache.Set(cacheKey, result, StandingsCacheDuration);

        return await WithCurrentPicksAsync(result, seasonId, cancellationToken);
    }

    /// <summary>
    /// Fills in each player's recent form: their picks from the last completed gameweeks,
    /// oldest first.
    /// </summary>
    private async Task AttachFormAsync(
        List<StandingEntryDto> standings, string seasonId, CancellationToken cancellationToken)
    {
        if (standings.Count == 0)
            return;

        var userIds = standings.Select(s => s.UserId).ToList();

        var picks = await _unitOfWork.Picks.FindAsync(
            p => p.SeasonId == seasonId && userIds.Contains(p.UserId),
            trackChanges: false, cancellationToken);

        var picksByUser = picks
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.GameweekNumber).ToList());

        if (picksByUser.Count == 0)
            return;

        var fixtures = await _unitOfWork.Fixtures.FindAsync(
            f => f.SeasonId == seasonId, trackChanges: false, cancellationToken);

        var fixturesByGameweek = fixtures
            .GroupBy(f => f.GameweekNumber)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<Fixture>)g.ToList());

        // Form counts whole gameweeks, not individual results. Taking a result the moment one
        // match ended would put half of the gameweek in progress into everyone's form and give
        // players different-length runs depending on when their team happened to play.
        var completedGameweeks = fixturesByGameweek
            .Where(g => g.Value.Count > 0 && g.Value.All(f => SettledStatuses.Contains(f.Status)))
            .Select(g => g.Key)
            .ToHashSet();

        var teams = (await _unitOfWork.Teams.FindAsync(
                t => true, trackChanges: false, cancellationToken))
            .ToDictionary(t => t.Id);

        foreach (var entry in standings)
        {
            if (!picksByUser.TryGetValue(entry.UserId, out var userPicks))
                continue;

            var form = new List<PickSummaryDto>(FormLength);

            // Walk back from the most recent gameweek, keeping only settled picks, then flip
            // to chronological order so the row reads left to right like a form guide.
            foreach (var pick in userPicks)
            {
                if (form.Count == FormLength)
                    break;

                if (!completedGameweeks.Contains(pick.GameweekNumber))
                    continue;

                if (!fixturesByGameweek.TryGetValue(pick.GameweekNumber, out var gameweekFixtures))
                    continue;

                var summary = PickSummaryFactory.Build(pick.GameweekNumber, pick.TeamId, gameweekFixtures, teams);

                // The gameweek is complete, so nothing here should be live or unplayed. The
                // remaining case is a pick on a team with no fixture that gameweek, which has
                // no result to show.
                if (summary is null || summary.IsLive || summary.Outcome == PickOutcome.Pending)
                    continue;

                form.Add(summary);
            }

            form.Reverse();
            entry.Form = form;
        }
    }

    /// <summary>
    /// Returns a copy of the standings with each player's pick for the in-progress gameweek
    /// attached, or the standings unchanged when there is nothing to reveal.
    /// </summary>
    /// <remarks>
    /// Picks stay hidden until the gameweek deadline passes. Before that they are private —
    /// seeing what everyone else has taken would change how you pick.
    /// </remarks>
    private async Task<LeagueStandingsDto> WithCurrentPicksAsync(
        LeagueStandingsDto standings, string seasonId, CancellationToken cancellationToken)
    {
        var gameweek = await ActiveGameweekFinder.FindAsync(_unitOfWork, seasonId, cancellationToken);
        if (gameweek == null)
            return standings;

        var picks = await _unitOfWork.Picks.FindAsync(
            p => p.SeasonId == seasonId && p.GameweekNumber == gameweek.WeekNumber,
            trackChanges: false, cancellationToken);

        var picksByUser = picks.ToDictionary(p => p.UserId);
        if (picksByUser.Count == 0)
            return standings;

        var fixtures = (await _unitOfWork.Fixtures.FindAsync(
            f => f.SeasonId == seasonId && f.GameweekNumber == gameweek.WeekNumber,
            trackChanges: false, cancellationToken)).ToList();

        var teamIds = fixtures
            .SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId })
            .Concat(picksByUser.Values.Select(p => p.TeamId))
            .Distinct()
            .ToList();

        var teams = (await _unitOfWork.Teams.FindAsync(
            t => teamIds.Contains(t.Id), trackChanges: false, cancellationToken))
            .ToDictionary(t => t.Id);

        var entries = standings.Standings
            .Select(entry =>
            {
                var copy = entry.Copy();
                if (picksByUser.TryGetValue(entry.UserId, out var pick))
                    copy.CurrentPick = PickSummaryFactory.Build(gameweek.WeekNumber, pick.TeamId, fixtures, teams);
                return copy;
            })
            .ToList();

        return new LeagueStandingsDto
        {
            Standings = entries,
            TotalPlayers = standings.TotalPlayers,
            LastUpdated = standings.LastUpdated
        };
    }
}
