using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Interfaces;

public interface IAdminService
{
    Task OverridePickAsync(Guid pickId, Guid newTeamId, string reason, CancellationToken cancellationToken = default);
    Task RecalculatePointsForGameweekAsync(Guid gameweekId, CancellationToken cancellationToken = default);
    Task RecalculateAllPointsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<AdminActionDto>> GetAdminActionsAsync(int limit = 50, CancellationToken cancellationToken = default);
    Task<BackfillPicksResponse> BackfillPicksAsync(Guid userId, List<BackfillPickRequest> picks, CancellationToken cancellationToken = default);

    // Season management
    Task<Guid> CreateSeasonAsync(CreateSeasonRequest request, CancellationToken cancellationToken = default);
    Task<IEnumerable<SeasonDto>> GetAllSeasonsAsync(CancellationToken cancellationToken = default);
    Task<SeasonDto?> GetActiveSeasonAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<TeamStatusDto>> GetTeamStatusesAsync(CancellationToken cancellationToken = default);
    Task UpdateTeamStatusAsync(Guid teamId, bool isActive, CancellationToken cancellationToken = default);
    Task<object> GetGameweeksDebugInfoAsync(CancellationToken cancellationToken = default);
}
