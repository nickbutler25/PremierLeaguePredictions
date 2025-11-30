using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;

namespace PremierLeaguePredictions.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PicksController : ControllerBase
{
    private readonly IPickService _pickService;
    private readonly ILogger<PicksController> _logger;

    public PicksController(
        IPickService pickService,
        ILogger<PicksController> logger)
    {
        _pickService = pickService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PickDto>>> GetMyPicks()
    {
        var userId = GetUserIdFromClaims();
        var picks = await _pickService.GetUserPicksAsync(userId);
        return Ok(picks);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PickDto>> GetPickById(Guid id)
    {
        var pick = await _pickService.GetPickByIdAsync(id);
        if (pick == null)
            return NotFound();

        var userId = GetUserIdFromClaims();
        if (pick.UserId != userId)
            return Forbid();

        return Ok(pick);
    }

    [HttpGet("gameweek/{seasonId}/{gameweekNumber}")]
    public async Task<ActionResult<IEnumerable<PickDto>>> GetPicksByGameweek(string seasonId, int gameweekNumber)
    {
        var picks = await _pickService.GetPicksByGameweekAsync(seasonId, gameweekNumber);
        return Ok(picks);
    }

    [HttpPost]
    [ServiceFilter(typeof(Filters.ValidationFilter<CreatePickRequest>))]
    public async Task<ActionResult<PickDto>> CreatePick([FromBody] CreatePickRequest request)
    {
        var userId = GetUserIdFromClaims();
        var pick = await _pickService.CreatePickAsync(userId, request);
        return CreatedAtAction(nameof(GetPickById), new { id = pick.Id }, pick);
    }

    [HttpPut("{id}")]
    [ServiceFilter(typeof(Filters.ValidationFilter<UpdatePickRequest>))]
    public async Task<ActionResult<PickDto>> UpdatePick(Guid id, [FromBody] UpdatePickRequest request)
    {
        var userId = GetUserIdFromClaims();
        var pick = await _pickService.UpdatePickAsync(id, userId, request);
        return Ok(pick);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePick(Guid id)
    {
        var userId = GetUserIdFromClaims();
        await _pickService.DeletePickAsync(id, userId);
        return NoContent();
    }

    private Guid GetUserIdFromClaims()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("Invalid user ID in token");
        return userId;
    }
}
