using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;

namespace PremierLeaguePredictions.API.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/teams")]
[Authorize(Roles = "Admin")]
public class AdminTeamsController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminTeamsController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpGet("status")]
    public async Task<ActionResult<ApiResponse<IEnumerable<TeamStatusDto>>>> GetTeamStatuses()
    {
        var teams = await _adminService.GetTeamStatusesAsync();
        return Ok(ApiResponse<IEnumerable<TeamStatusDto>>.SuccessResult(teams));
    }

    [HttpPut("{teamId}/status")]
    public async Task<IActionResult> UpdateTeamStatus(int teamId, [FromBody] UpdateTeamStatusRequest request)
    {
        await _adminService.UpdateTeamStatusAsync(teamId, request.IsActive);
        return NoContent();
    }
}

public class UpdateTeamStatusRequest
{
    public bool IsActive { get; set; }
}
