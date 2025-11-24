using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Application.Services;

public class EliminationService : IEliminationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EliminationService> _logger;

    public EliminationService(IUnitOfWork unitOfWork, ILogger<EliminationService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<List<UserEliminationDto>> GetSeasonEliminationsAsync(Guid seasonId, CancellationToken cancellationToken = default)
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
            var gameweek = await _unitOfWork.Gameweeks.GetByIdAsync(elimination.GameweekId, cancellationToken);
            var eliminatedBy = elimination.EliminatedBy.HasValue
                ? await _unitOfWork.Users.GetByIdAsync(elimination.EliminatedBy.Value, cancellationToken)
                : null;

            result.Add(new UserEliminationDto
            {
                Id = elimination.Id,
                UserId = elimination.UserId,
                UserName = user != null ? $"{user.FirstName} {user.LastName}" : "Unknown",
                SeasonId = elimination.SeasonId,
                GameweekId = elimination.GameweekId,
                GameweekNumber = gameweek?.WeekNumber ?? 0,
                Position = elimination.Position,
                TotalPoints = elimination.TotalPoints,
                EliminatedAt = elimination.EliminatedAt,
                EliminatedBy = elimination.EliminatedBy,
                EliminatedByName = eliminatedBy != null ? $"{eliminatedBy.FirstName} {eliminatedBy.LastName}" : null
            });
        }

        return result.OrderBy(e => e.GameweekNumber).ThenBy(e => e.Position).ToList();
    }

    public async Task<List<UserEliminationDto>> GetGameweekEliminationsAsync(Guid gameweekId, CancellationToken cancellationToken = default)
    {
        var eliminations = await _unitOfWork.UserEliminations.FindAsync(
            e => e.GameweekId == gameweekId,
            cancellationToken
        );

        var eliminationList = eliminations.ToList();
        var result = new List<UserEliminationDto>();

        foreach (var elimination in eliminationList)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(elimination.UserId, cancellationToken);
            var gameweek = await _unitOfWork.Gameweeks.GetByIdAsync(elimination.GameweekId, cancellationToken);
            var eliminatedBy = elimination.EliminatedBy.HasValue
                ? await _unitOfWork.Users.GetByIdAsync(elimination.EliminatedBy.Value, cancellationToken)
                : null;

            result.Add(new UserEliminationDto
            {
                Id = elimination.Id,
                UserId = elimination.UserId,
                UserName = user != null ? $"{user.FirstName} {user.LastName}" : "Unknown",
                SeasonId = elimination.SeasonId,
                GameweekId = elimination.GameweekId,
                GameweekNumber = gameweek?.WeekNumber ?? 0,
                Position = elimination.Position,
                TotalPoints = elimination.TotalPoints,
                EliminatedAt = elimination.EliminatedAt,
                EliminatedBy = elimination.EliminatedBy,
                EliminatedByName = eliminatedBy != null ? $"{eliminatedBy.FirstName} {eliminatedBy.LastName}" : null
            });
        }

        return result.OrderBy(e => e.Position).ToList();
    }

    public async Task<bool> IsUserEliminatedAsync(Guid userId, Guid seasonId, CancellationToken cancellationToken = default)
    {
        var elimination = await _unitOfWork.UserEliminations.FindAsync(
            e => e.UserId == userId && e.SeasonId == seasonId,
            cancellationToken
        );

        return elimination.Any();
    }

    public async Task<ProcessEliminationsResponse> ProcessGameweekEliminationsAsync(Guid gameweekId, Guid adminUserId, CancellationToken cancellationToken = default)
    {
        var response = new ProcessEliminationsResponse();

        var gameweek = await _unitOfWork.Gameweeks.GetByIdAsync(gameweekId, cancellationToken);
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
            e => e.GameweekId == gameweekId,
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

        var gameweekIds = allGameweeks.Select(g => g.Id).ToHashSet();

        var allPicks = await _unitOfWork.Picks.FindAsync(
            p => gameweekIds.Contains(p.GameweekId),
            cancellationToken
        );

        // Get already eliminated users
        var alreadyEliminated = await _unitOfWork.UserEliminations.FindAsync(
            e => e.SeasonId == gameweek.SeasonId && e.GameweekId != gameweekId,
            cancellationToken
        );

        var eliminatedUserIds = alreadyEliminated.Select(e => e.UserId).ToHashSet();

        // Calculate total points for each user (excluding already eliminated users)
        var userPoints = allPicks
            .Where(p => !eliminatedUserIds.Contains(p.UserId))
            .GroupBy(p => p.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalPoints = g.Sum(p => p.Points)
            })
            .OrderBy(u => u.TotalPoints)
            .ThenBy(u => u.UserId) // Tie-breaker: user ID
            .ToList();

        // Take bottom X players
        var usersToEliminate = userPoints.Take(gameweek.EliminationCount).ToList();

        _logger.LogInformation("Eliminating {Count} users from GW{WeekNumber}",
            usersToEliminate.Count, gameweek.WeekNumber);

        // Create elimination records
        int position = 1;
        foreach (var userToEliminate in usersToEliminate)
        {
            var elimination = new UserElimination
            {
                Id = Guid.NewGuid(),
                UserId = userToEliminate.UserId,
                SeasonId = gameweek.SeasonId,
                GameweekId = gameweekId,
                Position = position,
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
                GameweekId = elimination.GameweekId,
                GameweekNumber = gameweek.WeekNumber,
                Position = elimination.Position,
                TotalPoints = elimination.TotalPoints,
                EliminatedAt = elimination.EliminatedAt,
                EliminatedBy = elimination.EliminatedBy
            });

            position++;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        response.PlayersEliminated = usersToEliminate.Count;
        response.Message = $"Successfully eliminated {usersToEliminate.Count} player(s) from GW{gameweek.WeekNumber}";

        _logger.LogInformation("Elimination processing completed for GW{WeekNumber}", gameweek.WeekNumber);

        return response;
    }

    public async Task<List<EliminationConfigDto>> GetEliminationConfigsAsync(Guid seasonId, CancellationToken cancellationToken = default)
    {
        var gameweeks = await _unitOfWork.Gameweeks.FindAsync(
            g => g.SeasonId == seasonId,
            cancellationToken
        );

        var gameweekList = gameweeks.OrderBy(g => g.WeekNumber).ToList();
        var result = new List<EliminationConfigDto>();

        foreach (var gameweek in gameweekList)
        {
            var eliminations = await _unitOfWork.UserEliminations.FindAsync(
                e => e.GameweekId == gameweek.Id,
                cancellationToken
            );

            result.Add(new EliminationConfigDto
            {
                GameweekId = gameweek.Id,
                WeekNumber = gameweek.WeekNumber,
                EliminationCount = gameweek.EliminationCount,
                HasBeenProcessed = eliminations.Any(),
                Deadline = gameweek.Deadline
            });
        }

        return result;
    }

    public async Task UpdateGameweekEliminationCountAsync(Guid gameweekId, int eliminationCount, CancellationToken cancellationToken = default)
    {
        var gameweek = await _unitOfWork.Gameweeks.GetByIdAsync(gameweekId, cancellationToken);
        if (gameweek == null)
        {
            throw new ArgumentException("Gameweek not found", nameof(gameweekId));
        }

        // Check if eliminations have already been processed
        var existingEliminations = await _unitOfWork.UserEliminations.FindAsync(
            e => e.GameweekId == gameweekId,
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

    public async Task BulkUpdateEliminationCountsAsync(Dictionary<Guid, int> gameweekEliminationCounts, CancellationToken cancellationToken = default)
    {
        foreach (var kvp in gameweekEliminationCounts)
        {
            var gameweek = await _unitOfWork.Gameweeks.GetByIdAsync(kvp.Key, cancellationToken);
            if (gameweek == null)
            {
                _logger.LogWarning("Gameweek {GameweekId} not found, skipping", kvp.Key);
                continue;
            }

            // Check if eliminations have already been processed
            var existingEliminations = await _unitOfWork.UserEliminations.FindAsync(
                e => e.GameweekId == kvp.Key,
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
