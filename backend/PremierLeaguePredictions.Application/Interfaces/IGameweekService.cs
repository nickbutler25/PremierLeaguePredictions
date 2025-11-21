using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Interfaces;

public interface IGameweekService
{
    Task<GameweekDto?> GetGameweekByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<GameweekDto>> GetAllGameweeksAsync(CancellationToken cancellationToken = default);
    Task<GameweekDto?> GetCurrentGameweekAsync(CancellationToken cancellationToken = default);
    Task<GameweekWithFixturesDto?> GetGameweekWithFixturesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<GameweekDto>> GetGameweeksBySeasonIdAsync(Guid seasonId, CancellationToken cancellationToken = default);
}
