using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;

namespace PremierLeaguePredictions.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FixturesController : ControllerBase
{
    private readonly IFixtureService _fixtureService;
    private readonly IValidator<CreateFixtureRequest> _createValidator;
    private readonly IValidator<UpdateFixtureRequest> _updateValidator;
    private readonly ILogger<FixturesController> _logger;

    public FixturesController(
        IFixtureService fixtureService,
        IValidator<CreateFixtureRequest> createValidator,
        IValidator<UpdateFixtureRequest> updateValidator,
        ILogger<FixturesController> logger)
    {
        _fixtureService = fixtureService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FixtureDto>>> GetAllFixtures()
    {
        var fixtures = await _fixtureService.GetAllFixturesAsync();
        return Ok(fixtures);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FixtureDto>> GetFixtureById(Guid id)
    {
        var fixture = await _fixtureService.GetFixtureByIdAsync(id);
        if (fixture == null)
            return NotFound();
        return Ok(fixture);
    }

    [HttpGet("gameweek/{seasonId}/{gameweekNumber}")]
    public async Task<ActionResult<IEnumerable<FixtureDto>>> GetFixturesByGameweek(string seasonId, int gameweekNumber)
    {
        var fixtures = await _fixtureService.GetFixturesByGameweekAsync(seasonId, gameweekNumber);
        return Ok(fixtures);
    }

    [HttpGet("team/{teamId}")]
    public async Task<ActionResult<IEnumerable<FixtureDto>>> GetFixturesByTeam(int teamId)
    {
        var fixtures = await _fixtureService.GetFixturesByTeamIdAsync(teamId);
        return Ok(fixtures);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<FixtureDto>> CreateFixture([FromBody] CreateFixtureRequest request)
    {
        var validationResult = await _createValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
            return BadRequest(validationResult.Errors);

        var fixture = await _fixtureService.CreateFixtureAsync(request);
        return CreatedAtAction(nameof(GetFixtureById), new { id = fixture.Id }, fixture);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<FixtureDto>> UpdateFixture(Guid id, [FromBody] UpdateFixtureRequest request)
    {
        var validationResult = await _updateValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
            return BadRequest(validationResult.Errors);

        var fixture = await _fixtureService.UpdateFixtureAsync(id, request);
        return Ok(fixture);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteFixture(Guid id)
    {
        await _fixtureService.DeleteFixtureAsync(id);
        return NoContent();
    }

    [HttpPatch("{id}/scores")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateFixtureScores(Guid id, [FromBody] UpdateScoresRequest request)
    {
        await _fixtureService.UpdateFixtureScoresAsync(id, request.HomeScore, request.AwayScore);
        return NoContent();
    }
}

public class UpdateScoresRequest
{
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
}
