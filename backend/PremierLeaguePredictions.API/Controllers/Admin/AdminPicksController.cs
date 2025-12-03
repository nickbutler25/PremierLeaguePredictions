using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using PremierLeaguePredictions.API.Authorization;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Application.Services;

namespace PremierLeaguePredictions.API.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/picks")]
[Authorize(Policy = AdminPolicies.AdminOnly)]
public class AdminPicksController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IAutoPickService _autoPickService;
    private readonly IPickReminderService _pickReminderService;
    private readonly IAdminActionLogger _actionLogger;
    private readonly ILogger<AdminPicksController> _logger;

    public AdminPicksController(
        IAdminService adminService,
        IAutoPickService autoPickService,
        IPickReminderService pickReminderService,
        IAdminActionLogger actionLogger,
        ILogger<AdminPicksController> logger)
    {
        _adminService = adminService;
        _autoPickService = autoPickService;
        _pickReminderService = pickReminderService;
        _actionLogger = actionLogger;
        _logger = logger;
    }

    [HttpPost("{pickId}/override")]
    [Authorize(Policy = AdminPolicies.CriticalOperations)]
    public async Task<IActionResult> OverridePick(Guid pickId, [FromBody] OverridePickRequest request)
    {
        await _adminService.OverridePickAsync(pickId, request.NewTeamId, request.Reason);

        await _actionLogger.LogActionAsync(
            "OVERRIDE_PICK",
            new { pickId, newTeamId = request.NewTeamId, reason = request.Reason });

        return NoContent();
    }

    [HttpPost("backfill")]
    [Authorize(Policy = AdminPolicies.CriticalOperations)]
    public async Task<ActionResult<ApiResponse<BackfillPicksResponse>>> BackfillPicks([FromBody] BackfillPicksRequest request)
    {
        var response = await _adminService.BackfillPicksAsync(request.UserId, request.Picks);

        await _actionLogger.LogActionAsync(
            "BACKFILL_PICKS",
            new { userId = request.UserId, picksCount = request.Picks.Count },
            targetUserId: request.UserId);

        return Ok(ApiResponse<BackfillPicksResponse>.SuccessResult(response, "Picks backfilled successfully"));
    }

    [HttpPost("auto-assign/{seasonId}/{gameweekNumber}")]
    [Authorize(Policy = AdminPolicies.DataModification)]
    public async Task<ActionResult<ApiResponse<object>>> AutoAssignPicksForGameweek(string seasonId, int gameweekNumber)
    {
        try
        {
            var decodedSeasonId = Uri.UnescapeDataString(seasonId);
            await _autoPickService.AssignMissedPicksForGameweekAsync(decodedSeasonId, gameweekNumber);

            await _actionLogger.LogActionAsync(
                "AUTO_ASSIGN_PICKS",
                new { seasonId = decodedSeasonId, gameweekNumber },
                targetSeasonId: decodedSeasonId,
                targetGameweekNumber: gameweekNumber);

            return Ok(ApiResponse<object>.SuccessResult(new { }, "Auto-pick assignment completed successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-assign picks for gameweek {SeasonId}-{GameweekNumber}", seasonId, gameweekNumber);
            return StatusCode(500, ApiResponse<object>.FailureResult("Failed to auto-assign picks", new List<string> { ex.Message }));
        }
    }

    [HttpPost("auto-assign-all")]
    public async Task<ActionResult<ApiResponse<object>>> AutoAssignAllMissedPicks()
    {
        try
        {
            await _autoPickService.AssignAllMissedPicksAsync();
            return Ok(ApiResponse<object>.SuccessResult(new { }, "Auto-pick assignment completed for all gameweeks"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-assign all missed picks");
            return StatusCode(500, ApiResponse<object>.FailureResult("Failed to auto-assign picks", new List<string> { ex.Message }));
        }
    }

    [HttpPost("send-reminders")]
    public async Task<ActionResult<ApiResponse<object>>> SendPickReminders()
    {
        try
        {
            await _pickReminderService.SendPickRemindersAsync();
            return Ok(ApiResponse<object>.SuccessResult(new { }, "Pick reminders sent successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send pick reminders");
            return StatusCode(500, ApiResponse<object>.FailureResult("Failed to send pick reminders", new List<string> { ex.Message }));
        }
    }
}

public class OverridePickRequest
{
    public int NewTeamId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class BackfillPicksRequest
{
    public Guid UserId { get; set; }
    public List<BackfillPickRequest> Picks { get; set; } = new();
}
