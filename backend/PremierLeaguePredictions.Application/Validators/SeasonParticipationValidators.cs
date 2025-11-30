using FluentValidation;
using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Validators;

public class CreateSeasonParticipationRequestValidator : AbstractValidator<CreateSeasonParticipationRequest>
{
    public CreateSeasonParticipationRequestValidator()
    {
        RuleFor(x => x.SeasonId)
            .NotEmpty().WithMessage("Season ID is required")
            .MaximumLength(50).WithMessage("Season ID must not exceed 50 characters");
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
