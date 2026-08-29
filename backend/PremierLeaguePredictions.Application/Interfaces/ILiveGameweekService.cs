using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Interfaces;

public interface ILiveGameweekService
{
    /// <summary>
    /// One gameweek seen from one player's seat: the live one by default, or the gameweek
    /// asked for.
    /// </summary>
    /// <remarks>
    /// Only gameweeks whose deadline has passed can be served. Asking for one that has not
    /// locked returns a closed payload rather than throwing — the caller is not entitled to
    /// know the difference between "not yet" and "does not exist".
    /// </remarks>
    Task<LiveGameweekDto> GetGameweekAsync(
        Guid userId,
        string? seasonId = null,
        int? gameweekNumber = null,
        CancellationToken cancellationToken = default);
}
