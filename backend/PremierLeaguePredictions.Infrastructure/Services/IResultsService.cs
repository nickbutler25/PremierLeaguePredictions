using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Infrastructure.Services;

public interface IResultsService
{
    Task<ResultsSyncResponse> SyncRecentResultsAsync(CancellationToken cancellationToken = default);
    Task<ResultsSyncResponse> SyncGameweekResultsAsync(string seasonId, int gameweekNumber, CancellationToken cancellationToken = default);
}
