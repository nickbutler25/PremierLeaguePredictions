using FluentValidation;
using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Validators;

public class CreatePickRuleRequestValidator : AbstractValidator<CreatePickRuleRequest>
{
    public CreatePickRuleRequestValidator()
    {
        RuleFor(x => x.SeasonId)
            .NotEmpty().WithMessage("Season ID is required");

        RuleFor(x => x.Half)
            .InclusiveBetween(1, 2).WithMessage("Half must be 1 or 2");

        RuleFor(x => x.MaxTimesTeamCanBePicked)
            .GreaterThan(0).WithMessage("Max times team can be picked must be greater than 0")
            .LessThanOrEqualTo(19).WithMessage("Max times team can be picked cannot exceed 19 (one half of season)");

        RuleFor(x => x.MaxTimesOppositionCanBeTargeted)
            .GreaterThan(0).WithMessage("Max times opposition can be targeted must be greater than 0")
            .LessThanOrEqualTo(19).WithMessage("Max times opposition can be targeted cannot exceed 19 (one half of season)");
    }
}

public class UpdatePickRuleRequestValidator : AbstractValidator<UpdatePickRuleRequest>
{
    public UpdatePickRuleRequestValidator()
    {
        RuleFor(x => x.MaxTimesTeamCanBePicked)
            .GreaterThan(0).WithMessage("Max times team can be picked must be greater than 0")
            .LessThanOrEqualTo(19).WithMessage("Max times team can be picked cannot exceed 19 (one half of season)");

        RuleFor(x => x.MaxTimesOppositionCanBeTargeted)
            .GreaterThan(0).WithMessage("Max times opposition can be targeted must be greater than 0")
            .LessThanOrEqualTo(19).WithMessage("Max times opposition can be targeted cannot exceed 19 (one half of season)");
    }
}
