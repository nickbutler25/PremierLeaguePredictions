using Microsoft.AspNetCore.Mvc;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;

namespace PremierLeaguePredictions.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TeamsController : ControllerBase
{
    private readonly ITeamService _teamService;

    public TeamsController(ITeamService teamService)
    {
        _teamService = teamService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<TeamDto>>>> GetAllTeams()
    {
        var teams = await _teamService.GetAllTeamsAsync();
        return Ok(ApiResponse<IEnumerable<TeamDto>>.SuccessResult(teams));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<TeamDto>>> GetTeamById(Guid id)
    {
        var team = await _teamService.GetTeamByIdAsync(id);
        if (team == null)
            return NotFound(ApiResponse<TeamDto>.FailureResult("Team not found"));
        return Ok(ApiResponse<TeamDto>.SuccessResult(team));
    }
}
