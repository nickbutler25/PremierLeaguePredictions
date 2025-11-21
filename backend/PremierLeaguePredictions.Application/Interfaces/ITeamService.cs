using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Interfaces;

public interface ITeamService
{
    Task<TeamDto?> GetTeamByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<TeamDto>> GetAllTeamsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<TeamDto>> GetAvailableTeamsForGameweekAsync(Guid userId, Guid gameweekId, CancellationToken cancellationToken = default);
    Task<TeamDto> CreateTeamAsync(CreateTeamRequest request, CancellationToken cancellationToken = default);
    Task<TeamDto> UpdateTeamAsync(Guid id, UpdateTeamRequest request, CancellationToken cancellationToken = default);
    Task DeleteTeamAsync(Guid id, CancellationToken cancellationToken = default);
}
