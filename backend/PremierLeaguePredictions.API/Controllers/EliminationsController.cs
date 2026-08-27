using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;

namespace PremierLeaguePredictions.API.Controllers;

/// <summary>
/// The eliminations any player is allowed to see.
/// </summary>
/// <remarks>
/// Separate from the admin controller of the same name: this one reads, carries no admin trail,
/// and cannot process a gameweek.
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class EliminationsController : ControllerBase
{
    private readonly IEliminationService _eliminationService;

    public EliminationsController(IEliminationService eliminationService)
    {
        _eliminationService = eliminationService;
    }

    /// <summary>
    /// Who has gone out and when, and who the next gameweek threatens.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<EliminationsOverviewDto>>> GetOverview(
        [FromQuery] string? seasonId, CancellationToken cancellationToken)
    {
        var overview = await _eliminationService.GetEliminationsOverviewAsync(seasonId, cancellationToken);
        return Ok(ApiResponse<EliminationsOverviewDto>.SuccessResult(overview));
    }
}
