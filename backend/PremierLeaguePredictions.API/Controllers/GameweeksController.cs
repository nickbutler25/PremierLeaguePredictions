using Microsoft.AspNetCore.Mvc;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;

namespace PremierLeaguePredictions.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GameweeksController : ControllerBase
{
    private readonly IGameweekService _gameweekService;

    public GameweeksController(IGameweekService gameweekService)
    {
        _gameweekService = gameweekService;
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

    [HttpGet("{id}")]
    public async Task<ActionResult<GameweekWithFixturesDto>> GetGameweekById(Guid id)
    {
        var gameweek = await _gameweekService.GetGameweekWithFixturesAsync(id);
        if (gameweek == null)
            return NotFound();
        return Ok(gameweek);
    }
}
