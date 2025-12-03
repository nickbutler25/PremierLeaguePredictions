using FluentValidation;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Core.Constants;

namespace PremierLeaguePredictions.Application.Validators;

public class CreateSeasonRequestValidator : AbstractValidator<CreateSeasonRequest>
{
    public CreateSeasonRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Season name is required")
            .MaximumLength(ValidationRules.MaxSeasonNameLength).WithMessage($"Season name must not exceed {ValidationRules.MaxSeasonNameLength} characters");

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
            .LessThanOrEqualTo(GameRules.TotalGameweeks).WithMessage($"Gameweek number cannot exceed {GameRules.TotalGameweeks}");

        RuleFor(x => x.TeamId)
            .NotEmpty().WithMessage("Team ID is required");
    }
}
