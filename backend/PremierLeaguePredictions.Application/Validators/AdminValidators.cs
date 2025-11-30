using FluentValidation;
using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Validators;

public class CreateSeasonRequestValidator : AbstractValidator<CreateSeasonRequest>
{
    public CreateSeasonRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Season name is required")
            .MaximumLength(50).WithMessage("Season name must not exceed 50 characters");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("End date is required")
            .GreaterThan(x => x.StartDate).WithMessage("End date must be after start date");

        RuleFor(x => x.ExternalSeasonYear)
            .GreaterThan(2000).WithMessage("External season year must be greater than 2000")
            .LessThan(2100).WithMessage("External season year must be less than 2100");
    }
}

public class BackfillPickRequestValidator : AbstractValidator<BackfillPickRequest>
{
    public BackfillPickRequestValidator()
    {
        RuleFor(x => x.GameweekNumber)
            .GreaterThan(0).WithMessage("Gameweek number must be greater than 0")
            .LessThanOrEqualTo(38).WithMessage("Gameweek number cannot exceed 38");

        RuleFor(x => x.TeamId)
            .NotEmpty().WithMessage("Team ID is required");
    }
}
