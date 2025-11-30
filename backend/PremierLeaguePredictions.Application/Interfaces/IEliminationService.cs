using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Interfaces;

public interface IEliminationService
{
    /// <summary>
    /// Gets all eliminations for a specific season
    /// </summary>
    Task<List<UserEliminationDto>> GetSeasonEliminationsAsync(string seasonId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets eliminations for a specific gameweek
    /// </summary>
    Task<List<UserEliminationDto>> GetGameweekEliminationsAsync(string seasonId, int gameweekNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has been eliminated in the current season
    /// </summary>
    Task<bool> IsUserEliminatedAsync(Guid userId, string seasonId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes eliminations for a gameweek based on configured elimination count
    /// </summary>
    Task<ProcessEliminationsResponse> ProcessGameweekEliminationsAsync(string seasonId, int gameweekNumber, Guid adminUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets elimination configuration for all gameweeks in a season
    /// </summary>
    Task<List<EliminationConfigDto>> GetEliminationConfigsAsync(string seasonId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the elimination count for a specific gameweek
    /// </summary>
    Task UpdateGameweekEliminationCountAsync(string seasonId, int gameweekNumber, int eliminationCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk updates elimination counts for multiple gameweeks
    /// </summary>
    Task BulkUpdateEliminationCountsAsync(Dictionary<string, int> gameweekEliminationCounts, CancellationToken cancellationToken = default);
}
