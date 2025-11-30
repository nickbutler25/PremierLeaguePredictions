using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Interfaces;

public interface IFixtureService
{
    Task<FixtureDto?> GetFixtureByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<FixtureDto>> GetAllFixturesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<FixtureDto>> GetFixturesByGameweekAsync(string seasonId, int gameweekNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<FixtureDto>> GetFixturesByTeamIdAsync(int teamId, CancellationToken cancellationToken = default);
    Task<FixtureDto> CreateFixtureAsync(CreateFixtureRequest request, CancellationToken cancellationToken = default);
    Task<FixtureDto> UpdateFixtureAsync(Guid id, UpdateFixtureRequest request, CancellationToken cancellationToken = default);
    Task DeleteFixtureAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateFixtureScoresAsync(Guid id, int homeScore, int awayScore, CancellationToken cancellationToken = default);
}
