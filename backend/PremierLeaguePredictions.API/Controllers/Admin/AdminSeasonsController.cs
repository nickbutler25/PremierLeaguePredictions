using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Infrastructure.Services;

namespace PremierLeaguePredictions.API.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/seasons")]
[Authorize(Roles = "Admin")]
public class AdminSeasonsController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IFixtureSyncService _fixtureSyncService;

    public AdminSeasonsController(
        IAdminService adminService,
        IFixtureSyncService fixtureSyncService)
    {
        _adminService = adminService;
        _fixtureSyncService = fixtureSyncService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<SeasonDto>>>> GetSeasons()
    {
        var seasons = await _adminService.GetAllSeasonsAsync();
        return Ok(ApiResponse<IEnumerable<SeasonDto>>.SuccessResult(seasons));
    }

    [HttpGet("active")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<SeasonDto>>> GetActiveSeason()
    {
        var activeSeason = await _adminService.GetActiveSeasonAsync();

        if (activeSeason == null)
        {
            return NotFound(ApiResponse<SeasonDto>.FailureResult("No active season found"));
        }

        return Ok(ApiResponse<SeasonDto>.SuccessResult(activeSeason));
    }

    [HttpPost]
    [ServiceFilter(typeof(Filters.ValidationFilter<CreateSeasonRequest>))]
    public async Task<ActionResult<ApiResponse<CreateSeasonResponse>>> CreateSeason([FromBody] CreateSeasonRequest request)
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

            return Ok(ApiResponse<CreateSeasonResponse>.SuccessResult(response, response.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<CreateSeasonResponse>.FailureResult(ex.Message));
        }
    }
}
