namespace PremierLeaguePredictions.Application.DTOs;

public record PickRuleDto(
    Guid Id,
    string SeasonId,
    int Half,
    int MaxTimesTeamCanBePicked,
    int MaxTimesOppositionCanBeTargeted,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreatePickRuleRequest(
    string SeasonId,
    int Half,
    int MaxTimesTeamCanBePicked,
    int MaxTimesOppositionCanBeTargeted
);

public record UpdatePickRuleRequest(
    int MaxTimesTeamCanBePicked,
    int MaxTimesOppositionCanBeTargeted
);

public record PickRulesResponse(
    PickRuleDto? FirstHalf,
    PickRuleDto? SecondHalf
);
