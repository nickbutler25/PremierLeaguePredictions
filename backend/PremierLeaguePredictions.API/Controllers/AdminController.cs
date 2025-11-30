using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Infrastructure.Services;

namespace PremierLeaguePredictions.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IFixtureSyncService _fixtureSyncService;
    private readonly IResultsService _resultsService;
    private readonly IEliminationService _eliminationService;
    private readonly IAutoPickService _autoPickService;
    private readonly IPickReminderService _pickReminderService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IAdminService adminService,
        IFixtureSyncService fixtureSyncService,
        IResultsService resultsService,
        IEliminationService eliminationService,
        IAutoPickService autoPickService,
        IPickReminderService pickReminderService,
        ILogger<AdminController> logger)
    {
        _adminService = adminService;
        _fixtureSyncService = fixtureSyncService;
        _resultsService = resultsService;
        _eliminationService = eliminationService;
        _autoPickService = autoPickService;
        _pickReminderService = pickReminderService;
        _logger = logger;
    }

    [HttpPost("picks/{pickId}/override")]
    public async Task<IActionResult> OverridePick(Guid pickId, [FromBody] OverridePickRequest request)
    {
        await _adminService.OverridePickAsync(pickId, request.NewTeamId, request.Reason);
        return NoContent();
    }

    [HttpPost("gameweeks/{seasonId}/{gameweekNumber}/recalculate")]
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

    [HttpGet("actions")]
    public async Task<ActionResult<IEnumerable<AdminActionDto>>> GetAdminActions([FromQuery] int limit = 50)
    {
        var actions = await _adminService.GetAdminActionsAsync(limit);
        return Ok(actions);
    }

    [HttpPost("sync/teams")]
    public async Task<IActionResult> SyncTeams()
    {
        var (created, updated) = await _fixtureSyncService.SyncTeamsAsync();
        var teams = await _adminService.GetTeamStatusesAsync();
        var activeCount = teams.Count(t => t.IsActive);

        return Ok(new
        {
            message = $"Teams sync completed. Created: {created}, Updated: {updated}, Active: {activeCount}",
            teamsCreated = created,
            teamsUpdated = updated,
            totalActiveTeams = activeCount
        });
    }

    [HttpPost("sync/fixtures")]
    public async Task<IActionResult> SyncFixtures([FromQuery] int? season = null)
    {
        var (fixturesCreated, fixturesUpdated, gameweeksCreated) = await _fixtureSyncService.SyncFixturesAsync(season);
        var message = season.HasValue
            ? $"Fixtures sync completed for season {season}. Created: {fixturesCreated} fixtures, {gameweeksCreated} gameweeks. Updated: {fixturesUpdated} fixtures"
            : $"Fixtures sync completed for current season. Created: {fixturesCreated} fixtures, {gameweeksCreated} gameweeks. Updated: {fixturesUpdated} fixtures";

        return Ok(new
        {
            message,
            fixturesCreated,
            fixturesUpdated,
            gameweeksCreated
        });
    }

    [HttpPost("sync/results")]
    public async Task<ActionResult<ResultsSyncResponse>> SyncResults()
    {
        var response = await _resultsService.SyncRecentResultsAsync();
        return Ok(response);
    }

    [HttpPost("sync/results/gameweek/{seasonId}/{gameweekNumber}")]
    public async Task<ActionResult<ResultsSyncResponse>> SyncGameweekResults(string seasonId, int gameweekNumber)
    {
        var decodedSeasonId = Uri.UnescapeDataString(seasonId);
        var response = await _resultsService.SyncGameweekResultsAsync(decodedSeasonId, gameweekNumber);
        return Ok(response);
    }

    [HttpGet("seasons")]
    public async Task<ActionResult<IEnumerable<SeasonDto>>> GetSeasons()
    {
        var seasons = await _adminService.GetAllSeasonsAsync();
        return Ok(seasons);
    }

    [HttpGet("seasons/active")]
    [AllowAnonymous]
    public async Task<ActionResult<SeasonDto>> GetActiveSeason()
    {
        var activeSeason = await _adminService.GetActiveSeasonAsync();

        if (activeSeason == null)
        {
            return NotFound(new { message = "No active season found" });
        }

        return Ok(activeSeason);
    }

    [HttpPost("seasons")]
    [ServiceFilter(typeof(Filters.ValidationFilter<CreateSeasonRequest>))]
    public async Task<ActionResult<CreateSeasonResponse>> CreateSeason([FromBody] CreateSeasonRequest request)
    {
        try
        {
            // Step 1: Create the season
            var seasonId = await _adminService.CreateSeasonAsync(request);

            // Step 2: Sync teams from Football Data API
            var (teamsCreated, teamsUpdated) = await _fixtureSyncService.SyncTeamsAsync();

            // Step 3: Get team statuses to count active/inactive teams
            var allTeams = await _adminService.GetTeamStatusesAsync();
            var teamsActivated = allTeams.Count(t => t.IsActive);
            var teamsDeactivated = allTeams.Count(t => !t.IsActive);

            // Step 4: Sync fixtures for the specific season
            var (fixturesCreated, fixturesUpdated, gameweeksCreated) = await _fixtureSyncService.SyncFixturesAsync(request.ExternalSeasonYear);

            var response = new CreateSeasonResponse
            {
                SeasonId = seasonId,
                Message = $"Season '{request.Name}' created successfully with teams and fixtures synced.",
                TeamsCreated = teamsCreated,
                TeamsActivated = teamsActivated,
                TeamsDeactivated = teamsDeactivated,
                GameweeksCreated = gameweeksCreated,
                FixturesCreated = fixturesCreated
            };

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("teams/status")]
    public async Task<ActionResult<IEnumerable<TeamStatusDto>>> GetTeamStatuses()
    {
        var teams = await _adminService.GetTeamStatusesAsync();
        return Ok(teams);
    }

    [HttpPut("teams/{teamId}/status")]
    public async Task<IActionResult> UpdateTeamStatus(int teamId, [FromBody] UpdateTeamStatusRequest request)
    {
        await _adminService.UpdateTeamStatusAsync(teamId, request.IsActive);
        return NoContent();
    }

    [HttpPost("picks/backfill")]
    public async Task<ActionResult<BackfillPicksResponse>> BackfillPicks([FromBody] BackfillPicksRequest request)
    {
        var response = await _adminService.BackfillPicksAsync(request.UserId, request.Picks);
        return Ok(response);
    }

    [HttpGet("gameweeks/debug")]
    public async Task<ActionResult> GetGameweeksDebugInfo()
    {
        var gameweeksDebug = await _adminService.GetGameweeksDebugInfoAsync();
        return Ok(gameweeksDebug);
    }

    // Elimination management endpoints
    [HttpGet("eliminations/season/{seasonId}")]
    public async Task<ActionResult<List<UserEliminationDto>>> GetSeasonEliminations(string seasonId)
    {
        var decodedSeasonId = Uri.UnescapeDataString(seasonId);
        var eliminations = await _eliminationService.GetSeasonEliminationsAsync(decodedSeasonId);
        return Ok(eliminations);
    }

    [HttpGet("eliminations/gameweek/{seasonId}/{gameweekNumber}")]
    public async Task<ActionResult<List<UserEliminationDto>>> GetGameweekEliminations(string seasonId, int gameweekNumber)
    {
        var decodedSeasonId = Uri.UnescapeDataString(seasonId);
        var eliminations = await _eliminationService.GetGameweekEliminationsAsync(decodedSeasonId, gameweekNumber);
        return Ok(eliminations);
    }

    [HttpGet("eliminations/configs/{seasonId}")]
    public async Task<ActionResult<List<EliminationConfigDto>>> GetEliminationConfigs(string seasonId)
    {
        var decodedSeasonId = Uri.UnescapeDataString(seasonId);
        _logger.LogInformation("GetEliminationConfigs called for seasonId: {SeasonId}", decodedSeasonId);
        var configs = await _eliminationService.GetEliminationConfigsAsync(decodedSeasonId);
        _logger.LogInformation("Returned {Count} elimination configs for season {SeasonId}", configs.Count, decodedSeasonId);
        return Ok(configs);
    }

    [HttpPut("eliminations/gameweek/{seasonId}/{gameweekNumber}/count")]
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
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("eliminations/bulk-update")]
    public async Task<IActionResult> BulkUpdateEliminationCounts([FromBody] BulkUpdateEliminationCountsRequest request)
    {
        await _eliminationService.BulkUpdateEliminationCountsAsync(request.GameweekEliminationCounts);
        return NoContent();
    }

    [HttpPost("eliminations/process/{seasonId}/{gameweekNumber}")]
    public async Task<ActionResult<ProcessEliminationsResponse>> ProcessEliminations(string seasonId, int gameweekNumber)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var adminUserId))
        {
            return Unauthorized(new { message = "User ID not found in token" });
        }

        var decodedSeasonId = Uri.UnescapeDataString(seasonId);
        var response = await _eliminationService.ProcessGameweekEliminationsAsync(decodedSeasonId, gameweekNumber, adminUserId);
        return Ok(response);
    }

    // Auto-pick assignment endpoints
    [HttpPost("picks/auto-assign/{seasonId}/{gameweekNumber}")]
    public async Task<IActionResult> AutoAssignPicksForGameweek(string seasonId, int gameweekNumber)
    {
        try
        {
            var decodedSeasonId = Uri.UnescapeDataString(seasonId);
            await _autoPickService.AssignMissedPicksForGameweekAsync(decodedSeasonId, gameweekNumber);
            return Ok(new { message = "Auto-pick assignment completed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-assign picks for gameweek {SeasonId}-{GameweekNumber}", seasonId, gameweekNumber);
            return StatusCode(500, new { message = "Failed to auto-assign picks", error = ex.Message });
        }
    }

    [HttpPost("picks/auto-assign-all")]
    public async Task<IActionResult> AutoAssignAllMissedPicks()
    {
        try
        {
            await _autoPickService.AssignAllMissedPicksAsync();
            return Ok(new { message = "Auto-pick assignment completed for all gameweeks" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-assign all missed picks");
            return StatusCode(500, new { message = "Failed to auto-assign picks", error = ex.Message });
        }
    }

    // Pick reminder endpoints
    [HttpPost("picks/send-reminders")]
    public async Task<IActionResult> SendPickReminders()
    {
        try
        {
            await _pickReminderService.SendPickRemindersAsync();
            return Ok(new { message = "Pick reminders sent successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send pick reminders");
            return StatusCode(500, new { message = "Failed to send pick reminders", error = ex.Message });
        }
    }
}

public class OverridePickRequest
{
    public int NewTeamId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class UpdateTeamStatusRequest
{
    public bool IsActive { get; set; }
}

public class BackfillPicksRequest
{
    public Guid UserId { get; set; }
    public List<BackfillPickRequest> Picks { get; set; } = new();
}

public class UpdateEliminationCountRequest
{
    public int EliminationCount { get; set; }
}

public class BulkUpdateEliminationCountsRequest
{
    public Dictionary<string, int> GameweekEliminationCounts { get; set; } = new();
}
