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
    public async Task<ActionResult<IEnumerable<GameweekDto>>> GetAllGameweeks()
    {
        var gameweeks = await _gameweekService.GetAllGameweeksAsync();
        return Ok(gameweeks);
    }

    [HttpGet("current")]
    public async Task<ActionResult<GameweekDto>> GetCurrentGameweek()
    {
        var gameweek = await _gameweekService.GetCurrentGameweekAsync();
        if (gameweek == null)
            return NotFound();
        return Ok(gameweek);
    }

    [HttpGet("{seasonId}/{weekNumber}")]
    public async Task<ActionResult<GameweekWithFixturesDto>> GetGameweekById(string seasonId, int weekNumber)
    {
        var gameweek = await _gameweekService.GetGameweekWithFixturesAsync(seasonId, weekNumber);
        if (gameweek == null)
            return NotFound();
        return Ok(gameweek);
    }

    [HttpGet("pick-rules/{seasonId}")]
    public async Task<ActionResult<PickRulesResponse>> GetPickRules(string seasonId)
    {
        var decodedSeasonId = Uri.UnescapeDataString(seasonId);
        var rules = await _pickRuleService.GetPickRulesForSeasonAsync(decodedSeasonId);
        return Ok(rules);
    }
}
