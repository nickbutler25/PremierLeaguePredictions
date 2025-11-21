using FluentValidation;
using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Validators;

public class CreatePickRequestValidator : AbstractValidator<CreatePickRequest>
{
    public CreatePickRequestValidator()
    {
        RuleFor(x => x.GameweekId)
            .NotEmpty().WithMessage("Gameweek ID is required");

        RuleFor(x => x.TeamId)
            .NotEmpty().WithMessage("Team ID is required");
    }
}

public class UpdatePickRequestValidator : AbstractValidator<UpdatePickRequest>
{
    public UpdatePickRequestValidator()
    {
        RuleFor(x => x.TeamId)
            .NotEmpty().WithMessage("Team ID is required");
    }
}
