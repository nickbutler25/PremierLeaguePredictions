using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
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
    private readonly IValidator<CreatePickRequest> _createValidator;
    private readonly IValidator<UpdatePickRequest> _updateValidator;
    private readonly ILogger<PicksController> _logger;

    public PicksController(
        IPickService pickService,
        IValidator<CreatePickRequest> createValidator,
        IValidator<UpdatePickRequest> updateValidator,
        ILogger<PicksController> logger)
    {
        _pickService = pickService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
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

    [HttpGet("gameweek/{gameweekId}")]
    public async Task<ActionResult<IEnumerable<PickDto>>> GetPicksByGameweek(Guid gameweekId)
    {
        var picks = await _pickService.GetPicksByGameweekAsync(gameweekId);
        return Ok(picks);
    }

    [HttpPost]
    public async Task<ActionResult<PickDto>> CreatePick([FromBody] CreatePickRequest request)
    {
        var validationResult = await _createValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
            return BadRequest(validationResult.Errors);

        var userId = GetUserIdFromClaims();
        var pick = await _pickService.CreatePickAsync(userId, request);
        return CreatedAtAction(nameof(GetPickById), new { id = pick.Id }, pick);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<PickDto>> UpdatePick(Guid id, [FromBody] UpdatePickRequest request)
    {
        var validationResult = await _updateValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
            return BadRequest(validationResult.Errors);

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
