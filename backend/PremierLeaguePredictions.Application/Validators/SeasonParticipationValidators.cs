using FluentValidation;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Core.Constants;

namespace PremierLeaguePredictions.Application.Validators;

public class CreateSeasonParticipationRequestValidator : AbstractValidator<CreateSeasonParticipationRequest>
{
    public CreateSeasonParticipationRequestValidator()
    {
        RuleFor(x => x.SeasonId)
            .NotEmpty().WithMessage("Season ID is required")
            .MaximumLength(ValidationRules.MaxSeasonIdLength).WithMessage($"Season ID must not exceed {ValidationRules.MaxSeasonIdLength} characters");
    }
}

public class ApproveSeasonParticipationRequestValidator : AbstractValidator<ApproveSeasonParticipationRequest>
{
    public ApproveSeasonParticipationRequestValidator()
    {
        RuleFor(x => x.ParticipationId)
            .NotEmpty().WithMessage("Participation ID is required");
    }
}
