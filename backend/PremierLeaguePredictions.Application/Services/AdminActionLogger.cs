using System.Text.Json;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PremierLeaguePredictions.Application.Services;

public interface IAdminActionLogger
{
    /// <summary>
    /// Logs an admin action to the audit trail.
    /// </summary>
    /// <param name="actionType">Type of action (e.g., OVERRIDE_PICK, CREATE_SEASON)</param>
    /// <param name="details">Additional details about the action (will be serialized to JSON)</param>
    /// <param name="targetUserId">Optional user ID that the action affected</param>
    /// <param name="targetSeasonId">Optional season ID that the action affected</param>
    /// <param name="targetGameweekNumber">Optional gameweek number that the action affected</param>
    Task LogActionAsync(
        string actionType,
        object? details = null,
        Guid? targetUserId = null,
        string? targetSeasonId = null,
        int? targetGameweekNumber = null);
}

public class AdminActionLogger : IAdminActionLogger
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AdminActionLogger> _logger;

    public AdminActionLogger(
        IUnitOfWork unitOfWork,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AdminActionLogger> logger)
    {
        _unitOfWork = unitOfWork;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task LogActionAsync(
        string actionType,
        object? details = null,
        Guid? targetUserId = null,
        string? targetSeasonId = null,
        int? targetGameweekNumber = null)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                _logger.LogWarning("Cannot log admin action: HttpContext is null");
                return;
            }

            // Get admin user ID from claims
            var adminUserIdClaim = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(adminUserIdClaim) || !Guid.TryParse(adminUserIdClaim, out var adminUserId))
            {
                _logger.LogWarning("Cannot log admin action: Admin user ID not found in claims");
                return;
            }

            // Get IP address
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            // Serialize details to JSON
            var detailsJson = details != null
                ? JsonSerializer.Serialize(details, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                })
                : null;

            // Create enriched details with IP address
            var enrichedDetails = new
            {
                ipAddress,
                userAgent = httpContext.Request.Headers["User-Agent"].ToString(),
                timestamp = DateTime.UtcNow,
                originalDetails = details
            };

            var enrichedDetailsJson = JsonSerializer.Serialize(enrichedDetails, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var adminAction = new AdminAction
            {
                Id = Guid.NewGuid(),
                AdminUserId = adminUserId,
                ActionType = actionType,
                TargetUserId = targetUserId,
                TargetSeasonId = targetSeasonId,
                TargetGameweekNumber = targetGameweekNumber,
                Details = enrichedDetailsJson,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.AdminActions.AddAsync(adminAction);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "Admin action logged: {ActionType} by user {AdminUserId} from IP {IpAddress}",
                actionType,
                adminUserId,
                ipAddress);
        }
        catch (Exception ex)
        {
            // Log the error but don't throw - audit logging should not break the main operation
            _logger.LogError(ex, "Failed to log admin action: {ActionType}", actionType);
        }
    }
}
