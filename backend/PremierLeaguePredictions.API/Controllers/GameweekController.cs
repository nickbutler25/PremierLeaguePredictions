using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;

namespace PremierLeaguePredictions.API.Controllers;

/// <summary>
/// The gameweek in progress, from the signed-in player's point of view.
/// </summary>
/// <remarks>
/// Singular, and separate from <see cref="GameweeksController"/>, which serves gameweeks as
/// reference data. This one is per-viewer and reveals the whole field's picks, so it is
/// authorized and it only serves gameweeks whose deadline has passed.
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/gameweek")]
[Authorize]
public class GameweekController : ControllerBase
{
    private readonly ILiveGameweekService _liveGameweekService;

    public GameweekController(ILiveGameweekService liveGameweekService)
    {
        _liveGameweekService = liveGameweekService;
    }

    /// <summary>
    /// A gameweek: the viewer's pick, what the field picked, and who is closing on them.
    /// Defaults to the one being played, or the most recent one played.
    /// </summary>
    /// <remarks>
    /// Returns 200 with <c>isRevealed: false</c> rather than 404 when there is nothing to show,
    /// including when the gameweek asked for has not locked yet. A 404 there would confirm
    /// which gameweeks exist; more to the point, a closed window is a normal state rather than
    /// a missing resource, and the page needs the next deadline to count down to.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<LiveGameweekDto>>> Get(
        [FromQuery] string? seasonId,
        [FromQuery] int? gameweek,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromClaims();
        var result = await _liveGameweekService.GetGameweekAsync(
            userId, seasonId, gameweek, cancellationToken);
        return Ok(ApiResponse<LiveGameweekDto>.SuccessResult(result));
    }

    private Guid GetUserIdFromClaims()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("Invalid user ID in token");
        return userId;
    }
}
