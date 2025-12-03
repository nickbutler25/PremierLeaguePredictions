using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using PremierLeaguePredictions.API.Authorization;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Application.Services;
using PremierLeaguePredictions.Infrastructure.Services;

namespace PremierLeaguePredictions.API.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/sync")]
[Authorize(Policy = AdminPolicies.ExternalSync)]
public class AdminSyncController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IFixtureSyncService _fixtureSyncService;
    private readonly IResultsService _resultsService;
    private readonly IAdminActionLogger _actionLogger;

    public AdminSyncController(
        IAdminService adminService,
        IFixtureSyncService fixtureSyncService,
        IResultsService resultsService,
        IAdminActionLogger actionLogger)
    {
        _adminService = adminService;
        _fixtureSyncService = fixtureSyncService;
        _resultsService = resultsService;
        _actionLogger = actionLogger;
    }

    [HttpPost("teams")]
    public async Task<ActionResult<ApiResponse<object>>> SyncTeams()
    {
        var (created, updated) = await _fixtureSyncService.SyncTeamsAsync();
        var teams = await _adminService.GetTeamStatusesAsync();
        var activeCount = teams.Count(t => t.IsActive);

        var result = new
        {
            teamsCreated = created,
            teamsUpdated = updated,
            totalActiveTeams = activeCount
        };

        return Ok(ApiResponse<object>.SuccessResult(result, $"Teams sync completed. Created: {created}, Updated: {updated}, Active: {activeCount}"));
    }

    [HttpPost("fixtures")]
    public async Task<ActionResult<ApiResponse<object>>> SyncFixtures([FromQuery] int? season = null)
    {
        var (fixturesCreated, fixturesUpdated, gameweeksCreated) = await _fixtureSyncService.SyncFixturesAsync(season);
        var message = season.HasValue
            ? $"Fixtures sync completed for season {season}. Created: {fixturesCreated} fixtures, {gameweeksCreated} gameweeks. Updated: {fixturesUpdated} fixtures"
            : $"Fixtures sync completed for current season. Created: {fixturesCreated} fixtures, {gameweeksCreated} gameweeks. Updated: {fixturesUpdated} fixtures";

        var result = new
        {
            fixturesCreated,
            fixturesUpdated,
            gameweeksCreated
        };

        return Ok(ApiResponse<object>.SuccessResult(result, message));
    }

    [HttpPost("results")]
    public async Task<ActionResult<ApiResponse<ResultsSyncResponse>>> SyncResults()
    {
        var response = await _resultsService.SyncRecentResultsAsync();

        await _actionLogger.LogActionAsync(
            "SYNC_RESULTS",
            new { fixturesUpdated = response.FixturesUpdated, picksRecalculated = response.PicksRecalculated });

        return Ok(ApiResponse<ResultsSyncResponse>.SuccessResult(response, "Results synced successfully"));
    }

    [HttpPost("results/gameweek/{seasonId}/{gameweekNumber}")]
    public async Task<ActionResult<ApiResponse<ResultsSyncResponse>>> SyncGameweekResults(string seasonId, int gameweekNumber)
    {
        var decodedSeasonId = Uri.UnescapeDataString(seasonId);
        var response = await _resultsService.SyncGameweekResultsAsync(decodedSeasonId, gameweekNumber);
        return Ok(ApiResponse<ResultsSyncResponse>.SuccessResult(response, "Gameweek results synced successfully"));
    }
}
