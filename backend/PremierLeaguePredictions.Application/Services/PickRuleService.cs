using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Application.Services;

public class PickRuleService : IPickRuleService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PickRuleService> _logger;

    public PickRuleService(IUnitOfWork unitOfWork, ILogger<PickRuleService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PickRulesResponse> GetPickRulesForSeasonAsync(string seasonId)
    {
        var rules = await _unitOfWork.PickRules.FindAsync(r => r.SeasonId == seasonId);
        var rulesList = rules.ToList();

        var firstHalf = rulesList.FirstOrDefault(r => r.Half == 1);
        var secondHalf = rulesList.FirstOrDefault(r => r.Half == 2);

        return new PickRulesResponse(
            firstHalf != null ? MapToDto(firstHalf) : null,
            secondHalf != null ? MapToDto(secondHalf) : null
        );
    }

    public async Task<PickRuleDto> CreatePickRuleAsync(CreatePickRuleRequest request)
    {
        // Validate season exists
        var season = await _unitOfWork.Seasons.FirstOrDefaultAsync(s => s.Name == request.SeasonId);
        if (season == null)
        {
            throw new KeyNotFoundException($"Season {request.SeasonId} not found");
        }

        // Validate half is 1 or 2
        if (request.Half != 1 && request.Half != 2)
        {
            throw new ArgumentException("Half must be either 1 or 2");
        }

        // Check if rule already exists for this season/half
        var existing = await _unitOfWork.PickRules.FirstOrDefaultAsync(
            r => r.SeasonId == request.SeasonId && r.Half == request.Half);

        if (existing != null)
        {
            throw new InvalidOperationException($"Pick rule for {request.SeasonId} half {request.Half} already exists");
        }

        var pickRule = new PickRule
        {
            Id = Guid.NewGuid(),
            SeasonId = request.SeasonId,
            Half = request.Half,
            MaxTimesTeamCanBePicked = request.MaxTimesTeamCanBePicked,
            MaxTimesOppositionCanBeTargeted = request.MaxTimesOppositionCanBeTargeted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.PickRules.AddAsync(pickRule);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created pick rule for {SeasonId} half {Half}", request.SeasonId, request.Half);

        return MapToDto(pickRule);
    }

    public async Task<PickRuleDto> UpdatePickRuleAsync(Guid id, UpdatePickRuleRequest request)
    {
        var pickRule = await _unitOfWork.PickRules.GetByIdAsync(id);
        if (pickRule == null)
        {
            throw new KeyNotFoundException($"Pick rule {id} not found");
        }

        pickRule.MaxTimesTeamCanBePicked = request.MaxTimesTeamCanBePicked;
        pickRule.MaxTimesOppositionCanBeTargeted = request.MaxTimesOppositionCanBeTargeted;
        pickRule.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.PickRules.Update(pickRule);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Updated pick rule {Id} for {SeasonId} half {Half}",
            id, pickRule.SeasonId, pickRule.Half);

        return MapToDto(pickRule);
    }

    public async Task DeletePickRuleAsync(Guid id)
    {
        var pickRule = await _unitOfWork.PickRules.GetByIdAsync(id);
        if (pickRule == null)
        {
            throw new KeyNotFoundException($"Pick rule {id} not found");
        }

        _unitOfWork.PickRules.Remove(pickRule);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Deleted pick rule {Id} for {SeasonId} half {Half}",
            id, pickRule.SeasonId, pickRule.Half);
    }

    public async Task<PickRulesResponse> InitializeDefaultPickRulesAsync(string seasonId)
    {
        // Validate season exists
        var season = await _unitOfWork.Seasons.FirstOrDefaultAsync(s => s.Name == seasonId);
        if (season == null)
        {
            throw new KeyNotFoundException($"Season {seasonId} not found");
        }

        // Check if rules already exist
        var existing = await _unitOfWork.PickRules.FindAsync(r => r.SeasonId == seasonId);
        if (existing.Any())
        {
            throw new InvalidOperationException($"Pick rules for {seasonId} already exist");
        }

        // Create default rules for both halves
        var firstHalf = new PickRule
        {
            Id = Guid.NewGuid(),
            SeasonId = seasonId,
            Half = 1,
            MaxTimesTeamCanBePicked = 1,
            MaxTimesOppositionCanBeTargeted = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var secondHalf = new PickRule
        {
            Id = Guid.NewGuid(),
            SeasonId = seasonId,
            Half = 2,
            MaxTimesTeamCanBePicked = 1,
            MaxTimesOppositionCanBeTargeted = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.PickRules.AddAsync(firstHalf);
        await _unitOfWork.PickRules.AddAsync(secondHalf);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Initialized default pick rules for {SeasonId}", seasonId);

        return new PickRulesResponse(MapToDto(firstHalf), MapToDto(secondHalf));
    }

    private static PickRuleDto MapToDto(PickRule pickRule)
    {
        return new PickRuleDto(
            pickRule.Id,
            pickRule.SeasonId,
            pickRule.Half,
            pickRule.MaxTimesTeamCanBePicked,
            pickRule.MaxTimesOppositionCanBeTargeted,
            pickRule.CreatedAt,
            pickRule.UpdatedAt
        );
    }
}
