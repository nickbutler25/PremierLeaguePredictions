using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;

namespace PremierLeaguePredictions.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LeagueController : ControllerBase
{
    private readonly ILeagueService _leagueService;
    private readonly ILogger<LeagueController> _logger;

    public LeagueController(ILeagueService leagueService, ILogger<LeagueController> logger)
    {
        _leagueService = leagueService;
        _logger = logger;
    }

    [HttpGet("standings")]
    public async Task<ActionResult<LeagueStandingsDto>> GetStandings([FromQuery] string? seasonId = null)
    {
        var standings = await _leagueService.GetLeagueStandingsAsync(seasonId);
        return Ok(standings);
    }
}
