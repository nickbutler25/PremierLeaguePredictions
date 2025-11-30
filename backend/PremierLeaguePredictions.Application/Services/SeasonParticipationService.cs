using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Application.Services;

public class SeasonParticipationService : ISeasonParticipationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SeasonParticipationService> _logger;
    private readonly INotificationService _notificationService;

    public SeasonParticipationService(
        IUnitOfWork unitOfWork,
        ILogger<SeasonParticipationService> logger,
        INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task<SeasonParticipationDto> RequestParticipationAsync(Guid userId, string seasonId, CancellationToken cancellationToken = default)
    {
        // Check if user exists
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        // Check if season exists
        var season = await _unitOfWork.Seasons.FirstOrDefaultAsync(s => s.Name == seasonId, cancellationToken);
        if (season == null)
        {
            throw new InvalidOperationException("Season not found");
        }

        // Check if participation already exists
        var existingParticipations = await _unitOfWork.SeasonParticipations.FindAsync(
            sp => sp.UserId == userId && sp.SeasonId == seasonId,
            cancellationToken);

        var existingParticipation = existingParticipations.FirstOrDefault();
        if (existingParticipation != null)
        {
            _logger.LogInformation("User {UserId} already has participation request for season {SeasonId}", userId, seasonId);
            return await MapToDto(existingParticipation, cancellationToken);
        }

        // Create new participation request
        var participation = new SeasonParticipation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SeasonId = seasonId,
            IsApproved = false,
            RequestedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.SeasonParticipations.AddAsync(participation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} requested participation in season {SeasonId}", userId, seasonId);

        return await MapToDto(participation, cancellationToken);
    }

    public async Task<SeasonParticipationDto> ApproveParticipationAsync(Guid participationId, Guid adminUserId, bool isApproved, CancellationToken cancellationToken = default)
    {
        var participation = await _unitOfWork.SeasonParticipations.GetByIdAsync(participationId, cancellationToken);
        if (participation == null)
        {
            throw new InvalidOperationException("Participation request not found");
        }

        // Check if admin user exists
        var adminUser = await _unitOfWork.Users.GetByIdAsync(adminUserId, cancellationToken);
        if (adminUser == null || !adminUser.IsAdmin)
        {
            throw new UnauthorizedAccessException("Only admins can approve participation requests");
        }

        participation.IsApproved = isApproved;
        participation.ApprovedAt = DateTime.UtcNow;
        participation.ApprovedByUserId = adminUserId;
        participation.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.SeasonParticipations.Update(participation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Participation {ParticipationId} {Status} by admin {AdminUserId}",
            participationId, isApproved ? "approved" : "rejected", adminUserId);

        // Send SignalR notification and email to user
        var season = await _unitOfWork.Seasons.FirstOrDefaultAsync(s => s.Name == participation.SeasonId, cancellationToken);
        var user = await _unitOfWork.Users.GetByIdAsync(participation.UserId, cancellationToken);

        if (season != null && user != null)
        {
            // Send real-time SignalR notification
            await _notificationService.SendSeasonApprovalNotificationAsync(
                participation.UserId,
                isApproved,
                season.Name);

            // Send email notification
            await _notificationService.SendSeasonApprovalEmailAsync(
                user.Email,
                $"{user.FirstName} {user.LastName}",
                isApproved,
                season.Name);
        }

        return await MapToDto(participation, cancellationToken);
    }

    public async Task<IEnumerable<PendingApprovalDto>> GetPendingApprovalsAsync(string? seasonId = null, CancellationToken cancellationToken = default)
    {
        var query = await _unitOfWork.SeasonParticipations.FindAsync(
            sp => !sp.IsApproved && sp.ApprovedAt == null,
            cancellationToken);

        if (!string.IsNullOrEmpty(seasonId))
        {
            query = query.Where(sp => sp.SeasonId == seasonId);
        }

        var pendingList = new List<PendingApprovalDto>();

        foreach (var participation in query)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(participation.UserId, cancellationToken);
            var season = await _unitOfWork.Seasons.FirstOrDefaultAsync(s => s.Name == participation.SeasonId, cancellationToken);

            if (user != null && season != null)
            {
                pendingList.Add(new PendingApprovalDto
                {
                    ParticipationId = participation.Id,
                    UserId = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    PhotoUrl = user.PhotoUrl,
                    SeasonId = season.Name,
                    SeasonName = season.Name,
                    RequestedAt = participation.RequestedAt,
                    IsPaid = user.IsPaid
                });
            }
        }

        return pendingList.OrderBy(p => p.RequestedAt);
    }

    public async Task<IEnumerable<SeasonParticipationDto>> GetUserParticipationsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var participations = await _unitOfWork.SeasonParticipations.FindAsync(
            sp => sp.UserId == userId,
            cancellationToken);

        var dtos = new List<SeasonParticipationDto>();
        foreach (var participation in participations)
        {
            dtos.Add(await MapToDto(participation, cancellationToken));
        }

        return dtos;
    }

    public async Task<SeasonParticipationDto?> GetParticipationAsync(Guid userId, string seasonId, CancellationToken cancellationToken = default)
    {
        var participations = await _unitOfWork.SeasonParticipations.FindAsync(
            sp => sp.UserId == userId && sp.SeasonId == seasonId,
            cancellationToken);

        var participation = participations.FirstOrDefault();
        if (participation == null)
        {
            return null;
        }

        return await MapToDto(participation, cancellationToken);
    }

    public async Task<bool> IsUserApprovedForSeasonAsync(Guid userId, string seasonId, CancellationToken cancellationToken = default)
    {
        var participations = await _unitOfWork.SeasonParticipations.FindAsync(
            sp => sp.UserId == userId && sp.SeasonId == seasonId && sp.IsApproved,
            cancellationToken);

        return participations.Any();
    }

    private async Task<SeasonParticipationDto> MapToDto(SeasonParticipation participation, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(participation.UserId, cancellationToken);
        var season = await _unitOfWork.Seasons.FirstOrDefaultAsync(s => s.Name == participation.SeasonId, cancellationToken);

        string? approvedByUserName = null;
        if (participation.ApprovedByUserId.HasValue)
        {
            var approvedByUser = await _unitOfWork.Users.GetByIdAsync(participation.ApprovedByUserId.Value, cancellationToken);
            if (approvedByUser != null)
            {
                approvedByUserName = $"{approvedByUser.FirstName} {approvedByUser.LastName}";
            }
        }

        return new SeasonParticipationDto
        {
            Id = participation.Id,
            UserId = participation.UserId,
            SeasonId = participation.SeasonId,
            IsApproved = participation.IsApproved,
            RequestedAt = participation.RequestedAt,
            ApprovedAt = participation.ApprovedAt,
            ApprovedByUserId = participation.ApprovedByUserId,
            UserFirstName = user?.FirstName,
            UserLastName = user?.LastName,
            UserEmail = user?.Email,
            SeasonName = season?.Name,
            ApprovedByUserName = approvedByUserName
        };
    }
}
