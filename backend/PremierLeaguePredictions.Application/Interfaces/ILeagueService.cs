using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Interfaces;

public interface ILeagueService
{
    Task<LeagueStandingsDto> GetLeagueStandingsAsync(Guid? seasonId = null, CancellationToken cancellationToken = default);
}
