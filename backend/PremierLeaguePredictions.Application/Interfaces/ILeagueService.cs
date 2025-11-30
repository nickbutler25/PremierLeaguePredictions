using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Interfaces;

public interface ILeagueService
{
    Task<LeagueStandingsDto> GetLeagueStandingsAsync(string? seasonId = null, CancellationToken cancellationToken = default);
}
