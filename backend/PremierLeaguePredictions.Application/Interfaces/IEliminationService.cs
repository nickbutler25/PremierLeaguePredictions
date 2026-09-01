using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Interfaces;

public interface IEliminationService
{
    /// <summary>
    /// Gets all eliminations for a specific season
    /// </summary>
    Task<List<UserEliminationDto>> GetSeasonEliminationsAsync(string seasonId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The eliminations page: who has gone out, and who the next gameweek threatens.
    /// </summary>
    /// <remarks>
    /// Player-facing, so it leaves out the admin trail the admin view carries.
    /// </remarks>
    Task<EliminationsOverviewDto> GetEliminationsOverviewAsync(
        string? seasonId = null, CancellationToken cancellationToken = default);

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
    /// <param name="adminUserId">
    /// The admin who triggered the run, or <c>null</c> when nobody did — the scheduled completion
    /// job and the results sync both run unattended. It must be null rather than
    /// <see cref="Guid.Empty"/> in that case: UserElimination.EliminatedBy is a foreign key to
    /// users, and the empty guid is a value like any other, matching no row. Passing it made every
    /// automatic elimination fail on a 23503 foreign key violation.
    /// </param>
    Task<ProcessEliminationsResponse> ProcessGameweekEliminationsAsync(string seasonId, int gameweekNumber, Guid? adminUserId, CancellationToken cancellationToken = default);

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
