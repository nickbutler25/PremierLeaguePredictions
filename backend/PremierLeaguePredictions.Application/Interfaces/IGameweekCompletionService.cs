using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Interfaces;

/// <summary>
/// Finalises gameweeks whose football is over: locks them and processes eliminations.
/// </summary>
/// <remarks>
/// Triggered by a scheduled job a little after the last kickoff of each gameweek. It handles
/// every eligible gameweek rather than a named one, so a single run also picks up any earlier
/// gameweek left open by a postponement.
/// </remarks>
public interface IGameweekCompletionService
{
    Task<GameweekCompletionResponse> CompleteFinishedGameweeksAsync(CancellationToken cancellationToken = default);
}
