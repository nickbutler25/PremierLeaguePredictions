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
[Route("api/v{version:apiVersion}/admin/eliminations")]
[Authorize(Policy = AdminPolicies.AdminOnly)]
public class AdminEliminationsController : ControllerBase
{
    private readonly IEliminationService _eliminationService;
    private readonly IAdminActionLogger _actionLogger;
    private readonly ILogger<AdminEliminationsController> _logger;

    public AdminEliminationsController(
        IEliminationService eliminationService,
        IAdminActionLogger actionLogger,
        ILogger<AdminEliminationsController> logger)
    {
        _eliminationService = eliminationService;
        _actionLogger = actionLogger;
        _logger = logger;
    }

    [HttpGet("season/{seasonId}")]
    public async Task<ActionResult<ApiResponse<List<UserEliminationDto>>>> GetSeasonEliminations(string seasonId)
    {
        var decodedSeasonId = Uri.UnescapeDataString(seasonId);
        var eliminations = await _eliminationService.GetSeasonEliminationsAsync(decodedSeasonId);
        return Ok(ApiResponse<List<UserEliminationDto>>.SuccessResult(eliminations));
    }

    [HttpGet("gameweek/{seasonId}/{gameweekNumber}")]
    public async Task<ActionResult<ApiResponse<List<UserEliminationDto>>>> GetGameweekEliminations(string seasonId, int gameweekNumber)
    {
        var decodedSeasonId = Uri.UnescapeDataString(seasonId);
        var eliminations = await _eliminationService.GetGameweekEliminationsAsync(decodedSeasonId, gameweekNumber);
        return Ok(ApiResponse<List<UserEliminationDto>>.SuccessResult(eliminations));
    }

    [HttpGet("configs/{seasonId}")]
    public async Task<ActionResult<ApiResponse<List<EliminationConfigDto>>>> GetEliminationConfigs(string seasonId)
    {
        var decodedSeasonId = Uri.UnescapeDataString(seasonId);
        _logger.LogInformation("GetEliminationConfigs called for seasonId: {SeasonId}", decodedSeasonId);
        var configs = await _eliminationService.GetEliminationConfigsAsync(decodedSeasonId);
        _logger.LogInformation("Returned {Count} elimination configs for season {SeasonId}", configs.Count, decodedSeasonId);
        return Ok(ApiResponse<List<EliminationConfigDto>>.SuccessResult(configs));
    }

    [HttpPut("gameweek/{seasonId}/{gameweekNumber}/count")]
    public async Task<IActionResult> UpdateGameweekEliminationCount(string seasonId, int gameweekNumber, [FromBody] UpdateEliminationCountRequest request)
    {
        try
        {
            var decodedSeasonId = Uri.UnescapeDataString(seasonId);
            await _eliminationService.UpdateGameweekEliminationCountAsync(decodedSeasonId, gameweekNumber, request.EliminationCount);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponse<object>.FailureResult(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.FailureResult(ex.Message));
        }
    }

    [HttpPost("bulk-update")]
    public async Task<IActionResult> BulkUpdateEliminationCounts([FromBody] BulkUpdateEliminationCountsRequest request)
    {
        await _eliminationService.BulkUpdateEliminationCountsAsync(request.GameweekEliminationCounts);
        return NoContent();
    }

    [HttpPost("process/{seasonId}/{gameweekNumber}")]
    [Authorize(Policy = AdminPolicies.CriticalOperations)]
    public async Task<ActionResult<ApiResponse<ProcessEliminationsResponse>>> ProcessEliminations(string seasonId, int gameweekNumber)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var adminUserId))
        {
            return Unauthorized(ApiResponse<ProcessEliminationsResponse>.FailureResult("User ID not found in token"));
        }

        var decodedSeasonId = Uri.UnescapeDataString(seasonId);
        var response = await _eliminationService.ProcessGameweekEliminationsAsync(decodedSeasonId, gameweekNumber, adminUserId);

        await _actionLogger.LogActionAsync(
            "PROCESS_ELIMINATIONS",
            new { seasonId = decodedSeasonId, gameweekNumber, playersEliminated = response.PlayersEliminated },
            targetSeasonId: decodedSeasonId,
            targetGameweekNumber: gameweekNumber);

        return Ok(ApiResponse<ProcessEliminationsResponse>.SuccessResult(response, "Eliminations processed successfully"));
    }
}

public class UpdateEliminationCountRequest
{
    public int EliminationCount { get; set; }
}

public class BulkUpdateEliminationCountsRequest
{
    public Dictionary<string, int> GameweekEliminationCounts { get; set; } = new();
}
