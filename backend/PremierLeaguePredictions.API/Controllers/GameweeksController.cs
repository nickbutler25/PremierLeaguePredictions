using Microsoft.AspNetCore.Mvc;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;

namespace PremierLeaguePredictions.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GameweeksController : ControllerBase
{
    private readonly IGameweekService _gameweekService;
    private readonly IPickRuleService _pickRuleService;

    public GameweeksController(IGameweekService gameweekService, IPickRuleService pickRuleService)
    {
        _gameweekService = gameweekService;
        _pickRuleService = pickRuleService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<GameweekDto>>>> GetAllGameweeks()
    {
        var gameweeks = await _gameweekService.GetAllGameweeksAsync();
        return Ok(ApiResponse<IEnumerable<GameweekDto>>.SuccessResult(gameweeks));
    }

    [HttpGet("current")]
    public async Task<ActionResult<ApiResponse<GameweekDto>>> GetCurrentGameweek()
    {
        var gameweek = await _gameweekService.GetCurrentGameweekAsync();
        if (gameweek == null)
            return NotFound(ApiResponse<GameweekDto>.FailureResult("No current gameweek found"));
        return Ok(ApiResponse<GameweekDto>.SuccessResult(gameweek));
    }

    [HttpGet("{seasonId}/{weekNumber}")]
    public async Task<ActionResult<ApiResponse<GameweekWithFixturesDto>>> GetGameweekById(string seasonId, int weekNumber)
    {
        var gameweek = await _gameweekService.GetGameweekWithFixturesAsync(seasonId, weekNumber);
        if (gameweek == null)
            return NotFound(ApiResponse<GameweekWithFixturesDto>.FailureResult("Gameweek not found"));
        return Ok(ApiResponse<GameweekWithFixturesDto>.SuccessResult(gameweek));
    }

    [HttpGet("pick-rules/{seasonId}")]
    public async Task<ActionResult<ApiResponse<PickRulesResponse>>> GetPickRules(string seasonId)
    {
        var decodedSeasonId = Uri.UnescapeDataString(seasonId);
        var rules = await _pickRuleService.GetPickRulesForSeasonAsync(decodedSeasonId);
        return Ok(ApiResponse<PickRulesResponse>.SuccessResult(rules));
    }
}
