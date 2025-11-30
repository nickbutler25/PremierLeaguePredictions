using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Interfaces;

public interface IGameweekService
{
    Task<GameweekDto?> GetGameweekAsync(string seasonId, int weekNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<GameweekDto>> GetAllGameweeksAsync(CancellationToken cancellationToken = default);
    Task<GameweekDto?> GetCurrentGameweekAsync(CancellationToken cancellationToken = default);
    Task<GameweekWithFixturesDto?> GetGameweekWithFixturesAsync(string seasonId, int weekNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<GameweekDto>> GetGameweeksBySeasonIdAsync(string seasonId, CancellationToken cancellationToken = default);
}
