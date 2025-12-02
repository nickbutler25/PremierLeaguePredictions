using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using System.Security.Claims;

namespace PremierLeaguePredictions.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SeasonParticipationController : ControllerBase
{
    private readonly ISeasonParticipationService _seasonParticipationService;
    private readonly ILogger<SeasonParticipationController> _logger;

    public SeasonParticipationController(
        ISeasonParticipationService seasonParticipationService,
        ILogger<SeasonParticipationController> logger)
    {
        _seasonParticipationService = seasonParticipationService;
        _logger = logger;
    }

    [HttpPost("request")]
    public async Task<ActionResult<ApiResponse<SeasonParticipationDto>>> RequestParticipation([FromBody] CreateSeasonParticipationRequest request)
    {
        var userId = GetUserId();
        var participation = await _seasonParticipationService.RequestParticipationAsync(userId, request.SeasonId);
        return Ok(ApiResponse<SeasonParticipationDto>.SuccessResult(participation, "Participation request submitted successfully"));
    }

    [HttpPost("approve")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<SeasonParticipationDto>>> ApproveParticipation([FromBody] ApproveSeasonParticipationRequest request)
    {
        var adminUserId = GetUserId();
        var participation = await _seasonParticipationService.ApproveParticipationAsync(
            request.ParticipationId,
            adminUserId,
            request.IsApproved);
        return Ok(ApiResponse<SeasonParticipationDto>.SuccessResult(participation, request.IsApproved ? "Participation approved successfully" : "Participation rejected"));
    }

    [HttpGet("pending")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<IEnumerable<PendingApprovalDto>>>> GetPendingApprovals([FromQuery] string? seasonId = null)
    {
        var pendingApprovals = await _seasonParticipationService.GetPendingApprovalsAsync(seasonId);
        return Ok(ApiResponse<IEnumerable<PendingApprovalDto>>.SuccessResult(pendingApprovals));
    }

    [HttpGet("my-participations")]
    public async Task<ActionResult<ApiResponse<IEnumerable<SeasonParticipationDto>>>> GetMyParticipations()
    {
        var userId = GetUserId();
        var participations = await _seasonParticipationService.GetUserParticipationsAsync(userId);
        return Ok(ApiResponse<IEnumerable<SeasonParticipationDto>>.SuccessResult(participations));
    }

    [HttpGet("check")]
    public async Task<ActionResult<ApiResponse<bool>>> CheckParticipation([FromQuery] string seasonId)
    {
        var userId = GetUserId();
        var isApproved = await _seasonParticipationService.IsUserApprovedForSeasonAsync(userId, seasonId);
        return Ok(ApiResponse<bool>.SuccessResult(isApproved));
    }

    [HttpGet("participation")]
    public async Task<ActionResult<ApiResponse<SeasonParticipationDto>>> GetParticipation([FromQuery] string seasonId)
    {
        var userId = GetUserId();
        var participation = await _seasonParticipationService.GetParticipationAsync(userId, seasonId);
        if (participation == null)
        {
            return NotFound(ApiResponse<SeasonParticipationDto>.FailureResult("Participation not found"));
        }
        return Ok(ApiResponse<SeasonParticipationDto>.SuccessResult(participation));
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }
        return userId;
    }
}
