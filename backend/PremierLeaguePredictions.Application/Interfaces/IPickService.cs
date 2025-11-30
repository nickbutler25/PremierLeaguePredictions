using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Interfaces;

public interface IPickService
{
    Task<IEnumerable<PickDto>> GetUserPicksAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<PickDto?> GetPickByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PickDto> CreatePickAsync(Guid userId, CreatePickRequest request, CancellationToken cancellationToken = default);
    Task<PickDto> UpdatePickAsync(Guid id, Guid userId, UpdatePickRequest request, CancellationToken cancellationToken = default);
    Task DeletePickAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PickDto>> GetPicksByGameweekAsync(string seasonId, int gameweekNumber, CancellationToken cancellationToken = default);
}
