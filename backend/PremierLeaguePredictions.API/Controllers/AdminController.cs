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
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IAdminService adminService,
        IFixtureSyncService fixtureSyncService,
        ILogger<AdminController> logger)
    {
        _adminService = adminService;
        _fixtureSyncService = fixtureSyncService;
        _logger = logger;
    }

    [HttpPost("picks/{pickId}/override")]
    public async Task<IActionResult> OverridePick(Guid pickId, [FromBody] OverridePickRequest request)
    {
        await _adminService.OverridePickAsync(pickId, request.NewTeamId, request.Reason);
        return NoContent();
    }

    [HttpPost("gameweeks/{gameweekId}/recalculate")]
    public async Task<IActionResult> RecalculateGameweekPoints(Guid gameweekId)
    {
        await _adminService.RecalculatePointsForGameweekAsync(gameweekId);
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
    public async Task<IActionResult> SyncResults()
    {
        await _fixtureSyncService.SyncFixtureResultsAsync();
        return Ok(new { message = "Results sync completed" });
    }

    [HttpGet("seasons")]
    public async Task<ActionResult<IEnumerable<SeasonDto>>> GetSeasons()
    {
        var seasons = await _adminService.GetAllSeasonsAsync();
        return Ok(seasons);
    }

    [HttpPost("seasons")]
    public async Task<ActionResult<CreateSeasonResponse>> CreateSeason([FromBody] CreateSeasonRequest request)
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

    [HttpGet("teams/status")]
    public async Task<ActionResult<IEnumerable<TeamStatusDto>>> GetTeamStatuses()
    {
        var teams = await _adminService.GetTeamStatusesAsync();
        return Ok(teams);
    }

    [HttpPut("teams/{teamId}/status")]
    public async Task<IActionResult> UpdateTeamStatus(Guid teamId, [FromBody] UpdateTeamStatusRequest request)
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
}

public class OverridePickRequest
{
    public Guid NewTeamId { get; set; }
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
