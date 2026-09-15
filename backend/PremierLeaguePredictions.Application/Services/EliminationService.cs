using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Application.Services;

public class EliminationService : IEliminationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILeagueService _leagueService;
    private readonly ILogger<EliminationService> _logger;

    public EliminationService(
        IUnitOfWork unitOfWork, ILeagueService leagueService, ILogger<EliminationService> logger)
    {
        _unitOfWork = unitOfWork;
        _leagueService = leagueService;
        _logger = logger;
    }

    public async Task<List<UserEliminationDto>> GetSeasonEliminationsAsync(string seasonId, CancellationToken cancellationToken = default)
    {
        var eliminations = await _unitOfWork.UserEliminations.FindAsync(
            e => e.SeasonId == seasonId,
            cancellationToken
        );

        var eliminationList = eliminations.ToList();
        var result = new List<UserEliminationDto>();

        foreach (var elimination in eliminationList)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(elimination.UserId, cancellationToken);
            var gameweek = await _unitOfWork.Gameweeks.FirstOrDefaultAsync(g => g.SeasonId == elimination.SeasonId && g.WeekNumber == elimination.GameweekNumber, cancellationToken);
            var eliminatedBy = elimination.EliminatedBy.HasValue
                ? await _unitOfWork.Users.GetByIdAsync(elimination.EliminatedBy.Value, cancellationToken)
                : null;

            result.Add(new UserEliminationDto
            {
                Id = elimination.Id,
                UserId = elimination.UserId,
                UserName = user != null ? $"{user.FirstName} {user.LastName}" : "Unknown",
                SeasonId = elimination.SeasonId,
                GameweekNumber = elimination.GameweekNumber,
                Position = elimination.Position,
                TotalPoints = elimination.TotalPoints,
                EliminatedAt = elimination.EliminatedAt,
                EliminatedBy = elimination.EliminatedBy,
                EliminatedByName = eliminatedBy != null ? $"{eliminatedBy.FirstName} {eliminatedBy.LastName}" : null
            });
        }

        return result.OrderBy(e => e.GameweekNumber).ThenBy(e => e.Position).ToList();
    }

    public async Task<List<UserEliminationDto>> GetGameweekEliminationsAsync(string seasonId, int gameweekNumber, CancellationToken cancellationToken = default)
    {
        var eliminations = await _unitOfWork.UserEliminations.FindAsync(
            e => e.SeasonId == seasonId && e.GameweekNumber == gameweekNumber,
            cancellationToken
        );

        var eliminationList = eliminations.ToList();
        var result = new List<UserEliminationDto>();

        foreach (var elimination in eliminationList)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(elimination.UserId, cancellationToken);
            var gameweek = await _unitOfWork.Gameweeks.FirstOrDefaultAsync(g => g.SeasonId == elimination.SeasonId && g.WeekNumber == elimination.GameweekNumber, cancellationToken);
            var eliminatedBy = elimination.EliminatedBy.HasValue
                ? await _unitOfWork.Users.GetByIdAsync(elimination.EliminatedBy.Value, cancellationToken)
                : null;

            result.Add(new UserEliminationDto
            {
                Id = elimination.Id,
                UserId = elimination.UserId,
                UserName = user != null ? $"{user.FirstName} {user.LastName}" : "Unknown",
                SeasonId = elimination.SeasonId,

                GameweekNumber = elimination.GameweekNumber,
                Position = elimination.Position,
                TotalPoints = elimination.TotalPoints,
                EliminatedAt = elimination.EliminatedAt,
                EliminatedBy = elimination.EliminatedBy,
                EliminatedByName = eliminatedBy != null ? $"{eliminatedBy.FirstName} {eliminatedBy.LastName}" : null
            });
        }

        return result.OrderBy(e => e.Position).ToList();
    }

    public async Task<bool> IsUserEliminatedAsync(Guid userId, string seasonId, CancellationToken cancellationToken = default)
    {
        var elimination = await _unitOfWork.UserEliminations.FindAsync(
            e => e.UserId == userId && e.SeasonId == seasonId,
            cancellationToken
        );

        return elimination.Any();
    }

    public async Task<EliminationsOverviewDto> GetEliminationsOverviewAsync(
        string? seasonId = null, CancellationToken cancellationToken = default)
    {
        seasonId ??= (await _unitOfWork.Seasons.FindAsync(s => s.IsActive, trackChanges: false, cancellationToken))
            .FirstOrDefault()?.Name;

        var overview = new EliminationsOverviewDto { SeasonId = seasonId ?? string.Empty };
        if (string.IsNullOrEmpty(seasonId))
            return overview;

        // The standings already hold every approved player's record, eliminated ones included,
        // so the page cannot disagree with the table about anyone's points.
        var standings = await _leagueService.GetLeagueStandingsAsync(seasonId, cancellationToken);
        var entriesByUser = standings.Standings.ToDictionary(e => e.UserId);

        var eliminations = (await _unitOfWork.UserEliminations.FindAsync(
                e => e.SeasonId == seasonId, trackChanges: false, cancellationToken))
            .ToList();

        var userIds = standings.Standings.Select(e => e.UserId).ToList();
        var photoByUser = (await _unitOfWork.Users.FindAsync(
                u => userIds.Contains(u.Id), trackChanges: false, cancellationToken))
            .ToDictionary(u => u.Id, u => u.PhotoUrl);

        overview.TotalPlayers = standings.Standings.Count;
        overview.ActivePlayers = standings.Standings.Count(e => !e.IsEliminated);

        overview.Eliminated = eliminations
            .Where(e => entriesByUser.ContainsKey(e.UserId))
            .Select(e =>
            {
                var entry = entriesByUser[e.UserId];
                return new EliminatedPlayerDto
                {
                    UserId = e.UserId,
                    UserName = entry.UserName,
                    PhotoUrl = photoByUser.GetValueOrDefault(e.UserId),
                    GameweekNumber = e.GameweekNumber,
                    // The position recorded when they went out, never the live standings one.
                    // An eliminated player's season is over, so their place is settled — reading
                    // it from the current table would have it drift every time somebody still in
                    // scored.
                    FinalPosition = e.Position,
                    TotalPoints = entry.TotalPoints,
                    PicksMade = entry.PicksMade,
                    AveragePointsPerGame = Average(entry.TotalPoints, entry.PicksMade),
                    Wins = entry.Wins,
                    Draws = entry.Draws,
                    Losses = entry.Losses,
                    GoalsFor = entry.GoalsFor,
                    GoalsAgainst = entry.GoalsAgainst,
                    GoalDifference = entry.GoalDifference,
                    EliminatedAt = e.EliminatedAt
                };
            })
            // Latest casualties first, and within a gameweek the worst record first.
            .OrderByDescending(e => e.GameweekNumber)
            .ThenBy(e => e.AveragePointsPerGame)
            .ToList();

        overview.DangerZone = await BuildDangerZoneAsync(seasonId, standings, photoByUser, cancellationToken);

        return overview;
    }

    private static decimal Average(int totalPoints, int picksMade) =>
        picksMade == 0 ? 0m : Math.Round((decimal)totalPoints / picksMade, 2);

    /// <summary>
    /// Who the next elimination would take if the season stopped now.
    /// </summary>
    /// <remarks>
    /// Ranked the same way <see cref="ProcessGameweekEliminationsAsync"/> ranks, so the page and
    /// the run cannot disagree about who is in trouble.
    /// </remarks>
    private async Task<DangerZoneDto?> BuildDangerZoneAsync(
        string seasonId,
        LeagueStandingsDto standings,
        Dictionary<Guid, string?> photoByUser,
        CancellationToken cancellationToken)
    {
        var processedGameweeks = (await _unitOfWork.UserEliminations.FindAsync(
                e => e.SeasonId == seasonId, trackChanges: false, cancellationToken))
            .Select(e => e.GameweekNumber)
            .ToHashSet();

        var next = (await _unitOfWork.Gameweeks.FindAsync(
                g => g.SeasonId == seasonId && g.EliminationCount > 0, trackChanges: false, cancellationToken))
            .Where(g => !processedGameweeks.Contains(g.WeekNumber))
            .OrderBy(g => g.WeekNumber)
            .FirstOrDefault();

        if (next == null)
            return null; // nothing configured ahead — there is no zone to be in

        var contenders = standings.Standings
            .Where(e => !e.IsEliminated)
            .Select(e => new Contender(e, Average(e.TotalPoints, e.PicksMade)))
            .OrderBy(x => x.Entry.TotalPoints)
            .ThenBy(x => x.Average)
            .ThenBy(x => x.Entry.GoalDifference)
            .ThenBy(x => x.Entry.GoalsFor)
            .ThenBy(x => x.Entry.UserId)
            .ToList();

        var zone = new DangerZoneDto
        {
            GameweekNumber = next.WeekNumber,
            Deadline = next.Deadline,
            EliminationCount = next.EliminationCount,
            DeadlinePassed = next.Deadline <= DateTime.UtcNow
        };

        // The first player outside the zone is the bar the others have to clear.
        var safetyAverage = contenders.Count > next.EliminationCount
            ? contenders[next.EliminationCount].Average
            : (decimal?)null;

        var safetyPoints = contenders.Count > next.EliminationCount
            ? contenders[next.EliminationCount].Entry.TotalPoints
            : (int?)null;

        var inZone = contenders.Take(next.EliminationCount).ToList();

        AtRiskPlayerDto Map(Contender x, int pointsClear) => new AtRiskPlayerDto
        {
            UserId = x.Entry.UserId,
            UserName = x.Entry.UserName,
            PhotoUrl = photoByUser.GetValueOrDefault(x.Entry.UserId),
            Position = x.Entry.Position,
            TotalPoints = x.Entry.TotalPoints,
            PicksMade = x.Entry.PicksMade,
            AveragePointsPerGame = x.Average,
            Wins = x.Entry.Wins,
            Draws = x.Entry.Draws,
            Losses = x.Entry.Losses,
            GoalsFor = x.Entry.GoalsFor,
            GoalsAgainst = x.Entry.GoalsAgainst,
            GoalDifference = x.Entry.GoalDifference,
            PointsClearOfZone = pointsClear,
            PointsBehindSafety = safetyPoints.HasValue
                ? Math.Max(0, safetyPoints.Value - x.Entry.TotalPoints)
                : 0,
            AverageBehindSafety = safetyAverage.HasValue
                ? Math.Round(safetyAverage.Value - x.Average, 2)
                : 0m
        };

        zone.Players = inZone.Select(x => Map(x, 0)).ToList();
        zone.PointsFromDangerThreshold = PointsFromDangerThreshold;

        // Everyone still safe but close enough to be caught. Measured against the best player in
        // the zone, because that is who would overtake them first — the gap that actually decides
        // whether they stay up. With nobody in the zone there is no line to be near.
        var topOfZonePoints = inZone.Count > 0
            ? inZone.Max(x => x.Entry.TotalPoints)
            : (int?)null;

        if (topOfZonePoints.HasValue)
        {
            zone.JustSafe = contenders
                .Skip(next.EliminationCount)
                .Where(x => x.Entry.TotalPoints - topOfZonePoints.Value <= PointsFromDangerThreshold)
                .Select(x => Map(x, x.Entry.TotalPoints - topOfZonePoints.Value))
                .ToList();
        }

        return zone;
    }

    /// <summary>
    /// How close to the drop zone a safe player has to be before the danger zone lists them.
    /// </summary>
    private const int PointsFromDangerThreshold = 3;

    /// <summary>A player still in, with the average the elimination chain ranks on.</summary>
    private sealed record Contender(StandingEntryDto Entry, decimal Average);

    public async Task<ProcessEliminationsResponse> ProcessGameweekEliminationsAsync(string seasonId, int gameweekNumber, Guid? adminUserId, CancellationToken cancellationToken = default)
    {
        var response = new ProcessEliminationsResponse();

        var gameweek = await _unitOfWork.Gameweeks.FirstOrDefaultAsync(g => g.SeasonId == seasonId && g.WeekNumber == gameweekNumber, cancellationToken);
        if (gameweek == null)
        {
            response.Message = "Gameweek not found";
            return response;
        }

        if (gameweek.EliminationCount == 0)
        {
            response.Message = $"No eliminations configured for GW{gameweek.WeekNumber}";
            return response;
        }

        // Check if eliminations already processed for this gameweek
        var existingEliminations = await _unitOfWork.UserEliminations.FindAsync(
            e => e.SeasonId == seasonId && e.GameweekNumber == gameweekNumber,
            cancellationToken
        );

        if (existingEliminations.Any())
        {
            response.Message = $"Eliminations already processed for GW{gameweek.WeekNumber}";
            return response;
        }

        _logger.LogInformation("Processing eliminations for GW{WeekNumber}. Eliminating {Count} players",
            gameweek.WeekNumber, gameweek.EliminationCount);

        // Get all picks up to and including this gameweek
        var allGameweeks = await _unitOfWork.Gameweeks.FindAsync(
            g => g.SeasonId == gameweek.SeasonId && g.WeekNumber <= gameweek.WeekNumber,
            cancellationToken
        );

        var gameweekKeys = allGameweeks.Select(g => new { g.SeasonId, g.WeekNumber }).ToHashSet();

        var allPicks = await _unitOfWork.Picks.FindAsync(
            p => p.SeasonId == seasonId && p.GameweekNumber <= gameweekNumber,
            cancellationToken
        );

        // Get already eliminated users
        var alreadyEliminated = await _unitOfWork.UserEliminations.FindAsync(
            e => e.SeasonId == gameweek.SeasonId && e.GameweekNumber != gameweekNumber,
            cancellationToken
        );

        var eliminatedUserIds = alreadyEliminated.Select(e => e.UserId).ToHashSet();

        // A pick only counts as played once its fixture has a score to give, which is the same
        // test the standings use for picks made. Without it a player who joined late would be
        // ranked as though their missing gameweeks were nil scores.
        var fixtures = await _unitOfWork.Fixtures.FindAsync(
            f => f.SeasonId == seasonId && f.GameweekNumber <= gameweekNumber,
            trackChanges: false,
            cancellationToken);

        var playedTeamsByGameweek = fixtures
            .Where(f => f.Status is "FINISHED" or "IN_PLAY" or "PAUSED")
            .GroupBy(f => f.GameweekNumber)
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).ToHashSet());

        bool HasBeenPlayed(Pick pick) =>
            playedTeamsByGameweek.TryGetValue(pick.GameweekNumber, out var teams)
            && teams.Contains(pick.TeamId);

        // Ranked on points per gameweek played, not the raw total: the season's rule is the
        // lowest average, and the two only agree while everyone has played the same number.
        // Ranked from the approved players, not from the picks: grouping picks would leave out
        // anyone who has never made one, and a player with no record at all is precisely who the
        // rule is meant to catch.
        var approved = (await _unitOfWork.SeasonParticipations.FindAsync(
                sp => sp.SeasonId == seasonId && sp.IsApproved, trackChanges: false, cancellationToken))
            .Select(sp => sp.UserId)
            .Where(id => !eliminatedUserIds.Contains(id))
            .Distinct()
            .ToList();

        var picksByUser = allPicks
            .Where(p => !eliminatedUserIds.Contains(p.UserId))
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var userStandings = approved
            .Select(userId =>
            {
                var g = picksByUser.TryGetValue(userId, out var userPicks) ? userPicks : new List<Pick>();
                var gamesPlayed = g.Count(HasBeenPlayed);
                var totalPoints = g.Sum(p => p.Points);

                return new
                {
                    UserId = userId,
                    TotalPoints = totalPoints,
                    GamesPlayed = gamesPlayed,
                    // Nothing scored yet means there is no average to rank on. Zero puts them
                    // with the worst, which is where a player who has not scored belongs.
                    // Rounded to two places to match the figure the standings and the danger
                    // zone rank on, so all three agree about which players are actually level.
                    AveragePoints = gamesPlayed == 0
                        ? 0m
                        : Math.Round((decimal)totalPoints / gamesPlayed, 2),
                    GoalDifference = g.Sum(p => p.GoalsFor - p.GoalsAgainst),
                    GoalsFor = g.Sum(p => p.GoalsFor)
                };
            })
            // The league table's chain, read from the bottom: fewest points, then — only for
            // players level on points — the lower points per game, then goal difference, then
            // goals for. Points per game separates them only when a postponement has left the
            // field on different numbers of games; there, the same points off more games is the
            // worse record. The user id is a last resort so the result is stable, never a
            // decision.
            .OrderBy(u => u.TotalPoints)
            .ThenBy(u => u.AveragePoints)
            .ThenBy(u => u.GoalDifference)
            .ThenBy(u => u.GoalsFor)
            .ThenBy(u => u.UserId)
            .ToList();

        // Take bottom X players
        var usersToEliminate = userStandings.Take(gameweek.EliminationCount).ToList();

        _logger.LogInformation("Eliminating {Count} users from GW{WeekNumber}",
            usersToEliminate.Count, gameweek.WeekNumber);

        // Create elimination records.
        //
        // Position is the player's place in the league at the moment they go out, and is never
        // revisited: once eliminated, where they finished is settled and cannot move as the
        // players still in gain points.
        //
        // userStandings is worst-first among the players still in, so the worst of them finishes
        // last of that group. Anyone eliminated in an earlier gameweek sits below, having lasted
        // less long — which is why counting down from the number still in gives a position no
        // later run can collide with. With 266 in and 2 going out, they take 266 and 265;
        // the next run has 264 still in and its bottom player takes 264.
        //
        // It used to store 1, 2, 3 — the order within the batch — while being documented as a
        // league position, so the eliminations page showed the bottom two as having finished
        // first and second.
        var fieldStillIn = userStandings.Count;

        foreach (var (userToEliminate, index) in usersToEliminate.Select((u, i) => (u, i)))
        {
            var elimination = new UserElimination
            {
                Id = Guid.NewGuid(),
                UserId = userToEliminate.UserId,
                SeasonId = gameweek.SeasonId,
                GameweekNumber = gameweekNumber,
                Position = fieldStillIn - index,
                TotalPoints = userToEliminate.TotalPoints,
                EliminatedAt = DateTime.UtcNow,
                EliminatedBy = adminUserId
            };

            await _unitOfWork.UserEliminations.AddAsync(elimination, cancellationToken);

            var user = await _unitOfWork.Users.GetByIdAsync(userToEliminate.UserId, cancellationToken);

            response.EliminatedPlayers.Add(new UserEliminationDto
            {
                Id = elimination.Id,
                UserId = elimination.UserId,
                UserName = user != null ? $"{user.FirstName} {user.LastName}" : "Unknown",
                SeasonId = elimination.SeasonId,

                GameweekNumber = gameweek.WeekNumber,
                Position = elimination.Position,
                TotalPoints = elimination.TotalPoints,
                EliminatedAt = elimination.EliminatedAt,
                EliminatedBy = elimination.EliminatedBy
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Eliminations show in the standings, and the table hides eliminated players outright,
        // so the cached copy has to go or the run appears to have done nothing.
        _leagueService.InvalidateStandings(seasonId);

        response.PlayersEliminated = usersToEliminate.Count;
        response.Message = $"Successfully eliminated {usersToEliminate.Count} player(s) from GW{gameweek.WeekNumber}";

        _logger.LogInformation("Elimination processing completed for GW{WeekNumber}", gameweek.WeekNumber);

        return response;
    }

    public async Task<List<EliminationConfigDto>> GetEliminationConfigsAsync(string seasonId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting elimination configs for season: {SeasonId}", seasonId);

        var gameweeks = await _unitOfWork.Gameweeks.FindAsync(
            g => g.SeasonId == seasonId,
            cancellationToken
        );

        var gameweekList = gameweeks.OrderBy(g => g.WeekNumber).ToList();
        _logger.LogInformation("Found {Count} gameweeks for season {SeasonId}", gameweekList.Count, seasonId);
        var result = new List<EliminationConfigDto>();

        foreach (var gameweek in gameweekList)
        {
            var eliminations = await _unitOfWork.UserEliminations.FindAsync(
                e => e.SeasonId == gameweek.SeasonId && e.GameweekNumber == gameweek.WeekNumber,
                cancellationToken
            );

            result.Add(new EliminationConfigDto
            {
                GameweekId = $"{gameweek.SeasonId}-{gameweek.WeekNumber}",
                SeasonId = gameweek.SeasonId,
                WeekNumber = gameweek.WeekNumber,
                EliminationCount = gameweek.EliminationCount,
                HasBeenProcessed = eliminations.Any(),
                Deadline = gameweek.Deadline
            });
        }

        return result;
    }

    public async Task UpdateGameweekEliminationCountAsync(string seasonId, int gameweekNumber, int eliminationCount, CancellationToken cancellationToken = default)
    {
        var gameweek = await _unitOfWork.Gameweeks.FirstOrDefaultAsync(g => g.SeasonId == seasonId && g.WeekNumber == gameweekNumber, cancellationToken);
        if (gameweek == null)
        {
            throw new ArgumentException("Gameweek not found", nameof(gameweekNumber));
        }

        // Check if eliminations have already been processed
        var existingEliminations = await _unitOfWork.UserEliminations.FindAsync(
            e => e.SeasonId == seasonId && e.GameweekNumber == gameweekNumber,
            cancellationToken
        );

        if (existingEliminations.Any())
        {
            throw new InvalidOperationException("Cannot update elimination count after eliminations have been processed");
        }

        gameweek.EliminationCount = eliminationCount;
        gameweek.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Gameweeks.Update(gameweek);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated elimination count for GW{WeekNumber} to {Count}",
            gameweek.WeekNumber, eliminationCount);
    }

    public async Task BulkUpdateEliminationCountsAsync(Dictionary<string, int> gameweekEliminationCounts, CancellationToken cancellationToken = default)
    {
        foreach (var kvp in gameweekEliminationCounts)
        {
            // Parse the composite key format: "{SeasonId}-{WeekNumber}"
            var parts = kvp.Key.Split('-');
            if (parts.Length < 2 || !int.TryParse(parts[parts.Length - 1], out int weekNumber))
            {
                _logger.LogWarning("Invalid gameweek ID format: {GameweekId}, skipping", kvp.Key);
                continue;
            }

            // Reconstruct seasonId (handles case where seasonId contains dashes like "2024/2025")
            var seasonId = string.Join("-", parts.Take(parts.Length - 1));

            var gameweek = await _unitOfWork.Gameweeks.FirstOrDefaultAsync(
                g => g.SeasonId == seasonId && g.WeekNumber == weekNumber,
                cancellationToken);

            if (gameweek == null)
            {
                _logger.LogWarning("Gameweek {GameweekId} not found, skipping", kvp.Key);
                continue;
            }

            // Check if eliminations have already been processed
            var existingEliminations = await _unitOfWork.UserEliminations.FindAsync(
                e => e.SeasonId == gameweek.SeasonId && e.GameweekNumber == gameweek.WeekNumber,
                cancellationToken
            );

            if (existingEliminations.Any())
            {
                _logger.LogWarning("Eliminations already processed for GW{WeekNumber}, skipping", gameweek.WeekNumber);
                continue;
            }

            gameweek.EliminationCount = kvp.Value;
            gameweek.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Gameweeks.Update(gameweek);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Bulk updated elimination counts for {Count} gameweeks", gameweekEliminationCounts.Count);
    }
}
