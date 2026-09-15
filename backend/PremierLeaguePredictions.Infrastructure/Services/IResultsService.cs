using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Infrastructure.Services;

public interface IResultsService
{
    Task<ResultsSyncResponse> SyncRecentResultsAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Re-reads a gameweek's fixtures from the provider.
    /// </summary>
    /// <param name="reconcile">
    /// Ignore the settled filter and re-read every fixture that has kicked off, including ones
    /// already FINISHED. The routine sync stops watching a fixture shortly after it settles, so
    /// a score that was wrong at that moment stays wrong; this is the only way to go and check.
    /// Costs one call per fixture, so it is a deliberate operation, not something to schedule
    /// against the free tier's 10 calls/min.
    /// </param>
    Task<ResultsSyncResponse> SyncGameweekResultsAsync(
        string seasonId, int gameweekNumber, bool reconcile = false, CancellationToken cancellationToken = default);
}
