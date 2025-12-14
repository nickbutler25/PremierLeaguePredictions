using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Interfaces;

public interface IAutoPickService
{
    /// <summary>
    /// Auto-assign picks for users who missed the deadline for a specific gameweek
    /// </summary>
    /// <returns>Result containing the count of picks assigned and failed</returns>
    Task<AutoPickResult> AssignMissedPicksForGameweekAsync(string seasonId, int gameweekNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Auto-assign picks for all gameweeks with passed deadlines
    /// </summary>
    /// <returns>Result containing the count of picks assigned and failed across all gameweeks</returns>
    Task<AutoPickResult> AssignAllMissedPicksAsync(CancellationToken cancellationToken = default);
}
