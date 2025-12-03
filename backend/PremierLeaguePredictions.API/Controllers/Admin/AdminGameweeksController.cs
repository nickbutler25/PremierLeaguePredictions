using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;

namespace PremierLeaguePredictions.API.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/gameweeks")]
[Authorize(Roles = "Admin")]
public class AdminGameweeksController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminGameweeksController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpPost("{seasonId}/{gameweekNumber}/recalculate")]
    public async Task<IActionResult> RecalculateGameweekPoints(string seasonId, int gameweekNumber)
    {
        var decodedSeasonId = Uri.UnescapeDataString(seasonId);
        await _adminService.RecalculatePointsForGameweekAsync(decodedSeasonId, gameweekNumber);
        return NoContent();
    }

    [HttpPost("recalculate-all")]
    public async Task<IActionResult> RecalculateAllPoints()
    {
        await _adminService.RecalculateAllPointsAsync();
        return NoContent();
    }

    [HttpGet("debug")]
    public async Task<ActionResult<ApiResponse<object>>> GetGameweeksDebugInfo()
    {
        var gameweeksDebug = await _adminService.GetGameweeksDebugInfoAsync();
        return Ok(ApiResponse<object>.SuccessResult(gameweeksDebug));
    }

    [HttpGet("actions")]
    public async Task<ActionResult<ApiResponse<IEnumerable<AdminActionDto>>>> GetAdminActions([FromQuery] int limit = 50)
    {
        var actions = await _adminService.GetAdminActionsAsync(limit);
        return Ok(ApiResponse<IEnumerable<AdminActionDto>>.SuccessResult(actions));
    }
}
