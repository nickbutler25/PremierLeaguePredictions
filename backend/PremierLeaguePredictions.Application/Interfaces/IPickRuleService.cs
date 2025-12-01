using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Interfaces;

public interface IPickRuleService
{
    Task<PickRulesResponse> GetPickRulesForSeasonAsync(string seasonId);
    Task<PickRuleDto> CreatePickRuleAsync(CreatePickRuleRequest request);
    Task<PickRuleDto> UpdatePickRuleAsync(Guid id, UpdatePickRuleRequest request);
    Task DeletePickRuleAsync(Guid id);
    Task<PickRulesResponse> InitializeDefaultPickRulesAsync(string seasonId);
}
