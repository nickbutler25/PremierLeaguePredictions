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
    public async Task<ActionResult<SeasonParticipationDto>> RequestParticipation([FromBody] CreateSeasonParticipationRequest request)
    {
        var userId = GetUserId();
        var participation = await _seasonParticipationService.RequestParticipationAsync(userId, request.SeasonId);
        return Ok(participation);
    }

    [HttpPost("approve")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SeasonParticipationDto>> ApproveParticipation([FromBody] ApproveSeasonParticipationRequest request)
    {
        var adminUserId = GetUserId();
        var participation = await _seasonParticipationService.ApproveParticipationAsync(
            request.ParticipationId,
            adminUserId,
            request.IsApproved);
        return Ok(participation);
    }

    [HttpGet("pending")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<PendingApprovalDto>>> GetPendingApprovals([FromQuery] string? seasonId = null)
    {
        var pendingApprovals = await _seasonParticipationService.GetPendingApprovalsAsync(seasonId);
        return Ok(pendingApprovals);
    }

    [HttpGet("my-participations")]
    public async Task<ActionResult<IEnumerable<SeasonParticipationDto>>> GetMyParticipations()
    {
        var userId = GetUserId();
        var participations = await _seasonParticipationService.GetUserParticipationsAsync(userId);
        return Ok(participations);
    }

    [HttpGet("check")]
    public async Task<ActionResult<bool>> CheckParticipation([FromQuery] string seasonId)
    {
        var userId = GetUserId();
        var isApproved = await _seasonParticipationService.IsUserApprovedForSeasonAsync(userId, seasonId);
        return Ok(isApproved);
    }

    [HttpGet("participation")]
    public async Task<ActionResult<SeasonParticipationDto>> GetParticipation([FromQuery] string seasonId)
    {
        var userId = GetUserId();
        var participation = await _seasonParticipationService.GetParticipationAsync(userId, seasonId);
        if (participation == null)
        {
            return NotFound();
        }
        return Ok(participation);
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
