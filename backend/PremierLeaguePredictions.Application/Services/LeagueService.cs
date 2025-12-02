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

    public LeagueService(IUnitOfWork unitOfWork, ILogger<LeagueService> logger, IMemoryCache cache)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _cache = cache;
    }

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

        // Check cache first
        var cacheKey = $"standings_{seasonId}";
        if (_cache.TryGetValue(cacheKey, out LeagueStandingsDto? cachedStandings) && cachedStandings != null)
        {
            _logger.LogDebug("Returning standings from cache for season {SeasonId}", seasonId);
            return cachedStandings;
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
        var sortedStandings = standings
            .OrderByDescending(s => s.TotalPoints)
            .ThenByDescending(s => s.GoalDifference)
            .ThenByDescending(s => s.GoalsFor)
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

        // Cache the result
        _cache.Set(cacheKey, result, StandingsCacheDuration);

        return result;
    }
}
