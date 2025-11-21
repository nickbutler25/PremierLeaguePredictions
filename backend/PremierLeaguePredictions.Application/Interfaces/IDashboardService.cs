using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardDto> GetUserDashboardAsync(Guid userId, CancellationToken cancellationToken = default);
}
