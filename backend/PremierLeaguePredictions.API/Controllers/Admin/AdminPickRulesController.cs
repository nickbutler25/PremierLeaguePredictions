using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;

namespace PremierLeaguePredictions.API.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/pick-rules")]
[Authorize(Roles = "Admin")]
public class AdminPickRulesController : ControllerBase
{
    private readonly IPickRuleService _pickRuleService;

    public AdminPickRulesController(IPickRuleService pickRuleService)
    {
        _pickRuleService = pickRuleService;
    }

    [HttpGet("{seasonId}")]
    public async Task<ActionResult<ApiResponse<PickRulesResponse>>> GetPickRules(string seasonId)
    {
        var decodedSeasonId = Uri.UnescapeDataString(seasonId);
        var rules = await _pickRuleService.GetPickRulesForSeasonAsync(decodedSeasonId);
        return Ok(ApiResponse<PickRulesResponse>.SuccessResult(rules));
    }

    [HttpPost]
    [ServiceFilter(typeof(Filters.ValidationFilter<CreatePickRuleRequest>))]
    public async Task<ActionResult<ApiResponse<PickRuleDto>>> CreatePickRule([FromBody] CreatePickRuleRequest request)
    {
        var rule = await _pickRuleService.CreatePickRuleAsync(request);
        return CreatedAtAction(nameof(GetPickRules), new { seasonId = rule.SeasonId }, ApiResponse<PickRuleDto>.SuccessResult(rule, "Pick rule created successfully"));
    }

    [HttpPut("{id}")]
    [ServiceFilter(typeof(Filters.ValidationFilter<UpdatePickRuleRequest>))]
    public async Task<ActionResult<ApiResponse<PickRuleDto>>> UpdatePickRule(Guid id, [FromBody] UpdatePickRuleRequest request)
    {
        var rule = await _pickRuleService.UpdatePickRuleAsync(id, request);
        return Ok(ApiResponse<PickRuleDto>.SuccessResult(rule, "Pick rule updated successfully"));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePickRule(Guid id)
    {
        await _pickRuleService.DeletePickRuleAsync(id);
        return NoContent();
    }

    [HttpPost("{seasonId}/initialize")]
    public async Task<ActionResult<ApiResponse<PickRulesResponse>>> InitializeDefaultPickRules(string seasonId)
    {
        var decodedSeasonId = Uri.UnescapeDataString(seasonId);
        var rules = await _pickRuleService.InitializeDefaultPickRulesAsync(decodedSeasonId);
        return Ok(ApiResponse<PickRulesResponse>.SuccessResult(rules, "Default pick rules initialized successfully"));
    }
}
