using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using System.Security.Claims;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;

namespace PremierLeaguePredictions.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(IDashboardService dashboardService, ILogger<DashboardController> logger)
    {
        _dashboardService = dashboardService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<DashboardDto>>> GetMyDashboard()
    {
        var userId = GetUserIdFromClaims();
        var dashboard = await _dashboardService.GetUserDashboardAsync(userId);
        return Ok(ApiResponse<DashboardDto>.SuccessResult(dashboard));
    }

    [HttpGet("{userId}")]
    public async Task<ActionResult<ApiResponse<DashboardDto>>> GetUserDashboard(Guid userId)
    {
        var currentUserId = GetUserIdFromClaims();
        var isAdmin = User.IsInRole("Admin");

        if (userId != currentUserId && !isAdmin)
            return Forbid();

        var dashboard = await _dashboardService.GetUserDashboardAsync(userId);
        return Ok(ApiResponse<DashboardDto>.SuccessResult(dashboard));
    }

    private Guid GetUserIdFromClaims()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("Invalid user ID in token");
        return userId;
    }
}
