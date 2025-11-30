using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Interfaces;

public interface ISeasonParticipationService
{
    Task<SeasonParticipationDto> RequestParticipationAsync(Guid userId, string seasonId, CancellationToken cancellationToken = default);
    Task<SeasonParticipationDto> ApproveParticipationAsync(Guid participationId, Guid adminUserId, bool isApproved, CancellationToken cancellationToken = default);
    Task<IEnumerable<PendingApprovalDto>> GetPendingApprovalsAsync(string? seasonId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<SeasonParticipationDto>> GetUserParticipationsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<SeasonParticipationDto?> GetParticipationAsync(Guid userId, string seasonId, CancellationToken cancellationToken = default);
    Task<bool> IsUserApprovedForSeasonAsync(Guid userId, string seasonId, CancellationToken cancellationToken = default);
}
