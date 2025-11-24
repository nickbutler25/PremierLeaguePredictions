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

    public LeagueService(IUnitOfWork unitOfWork, ILogger<LeagueService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<LeagueStandingsDto> GetLeagueStandingsAsync(Guid? seasonId = null, CancellationToken cancellationToken = default)
    {
        var allUsers = await _unitOfWork.Users.GetAllAsync(cancellationToken);
        var allPicks = await _unitOfWork.Picks.GetAllAsync(cancellationToken);
        var allGameweeks = await _unitOfWork.Gameweeks.GetAllAsync(cancellationToken);

        // Get active season if not specified
        Season? activeSeason = null;
        if (!seasonId.HasValue)
        {
            var seasons = await _unitOfWork.Seasons.FindAsync(s => s.IsActive, cancellationToken);
            activeSeason = seasons.FirstOrDefault();
            seasonId = activeSeason?.Id;
        }
        else
        {
            activeSeason = await _unitOfWork.Seasons.GetByIdAsync(seasonId.Value, cancellationToken);
        }

        // Get eliminations for the season
        var eliminations = seasonId.HasValue
            ? await _unitOfWork.UserEliminations.FindAsync(e => e.SeasonId == seasonId.Value, cancellationToken)
            : new List<Core.Entities.UserElimination>().AsEnumerable();

        var eliminationsByUser = eliminations.ToDictionary(e => e.UserId, e => e);
        var gameweeksDict = allGameweeks.ToDictionary(g => g.Id, g => g);

        // Create a set of gameweek IDs that have passed their deadline
        var completedGameweekIds = allGameweeks
            .Where(g => g.Deadline < DateTime.UtcNow)
            .Select(g => g.Id)
            .ToHashSet();

        var userStandings = allUsers.Select(user =>
        {
            var userPicks = allPicks.Where(p => p.UserId == user.Id).ToList();

            // Only count picks in completed gameweeks
            var completedPicks = userPicks.Where(p => completedGameweekIds.Contains(p.GameweekId)).ToList();

            var totalPoints = userPicks.Sum(p => p.Points);
            var picksMade = completedPicks.Count; // Only count completed picks as "played"
            var wins = completedPicks.Count(p => p.Points == 3);
            var draws = completedPicks.Count(p => p.Points == 1);
            var losses = completedPicks.Count(p => p.Points == 0);

            // Calculate goals (only from completed picks)
            var goalsFor = completedPicks.Sum(p => p.GoalsFor);
            var goalsAgainst = completedPicks.Sum(p => p.GoalsAgainst);
            var goalDifference = goalsFor - goalsAgainst;

            // Check elimination status
            var isEliminated = eliminationsByUser.TryGetValue(user.Id, out var elimination);
            int? eliminatedInGameweek = null;
            int? eliminationPosition = null;

            if (isEliminated && elimination != null)
            {
                if (gameweeksDict.TryGetValue(elimination.GameweekId, out var gameweek))
                {
                    eliminatedInGameweek = gameweek.WeekNumber;
                }
                eliminationPosition = elimination.Position;
            }

            return new StandingEntryDto
            {
                UserId = user.Id,
                UserName = $"{user.FirstName} {user.LastName}",
                TotalPoints = totalPoints,
                PicksMade = picksMade,
                Wins = wins,
                Draws = draws,
                Losses = losses,
                GoalsFor = goalsFor,
                GoalsAgainst = goalsAgainst,
                GoalDifference = goalDifference,
                IsEliminated = isEliminated,
                EliminatedInGameweek = eliminatedInGameweek,
                EliminationPosition = eliminationPosition,
                Position = 0, // Will be calculated after sorting
                Rank = 0 // Will be calculated after sorting
            };
        })
        .OrderByDescending(s => s.TotalPoints)
        .ThenByDescending(s => s.GoalDifference)
        .ThenByDescending(s => s.GoalsFor)
        .ToList();

        // Assign positions and ranks
        for (int i = 0; i < userStandings.Count; i++)
        {
            userStandings[i].Position = i + 1;
            userStandings[i].Rank = i + 1;
        }

        return new LeagueStandingsDto
        {
            Standings = userStandings,
            TotalPlayers = userStandings.Count,
            LastUpdated = DateTime.UtcNow
        };
    }
}
