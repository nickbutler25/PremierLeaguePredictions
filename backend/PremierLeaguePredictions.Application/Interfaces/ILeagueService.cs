using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Interfaces;

public interface ILeagueService
{
    Task<LeagueStandingsDto> GetLeagueStandingsAsync(string? seasonId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops the cached standings for a season so the next request recomputes them.
    /// </summary>
    /// <remarks>
    /// Call this whenever a result changes. Clients are told about score changes over SignalR
    /// and refetch immediately, but a refetch that hits a cached response returns the same
    /// stale numbers — the push is only as live as the cache behind it.
    /// </remarks>
    void InvalidateStandings(string seasonId);
}
