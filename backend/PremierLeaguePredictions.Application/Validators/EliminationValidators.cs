using FluentValidation;
using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Validators;

public class UpdateGameweekEliminationRequestValidator : AbstractValidator<UpdateGameweekEliminationRequest>
{
    public UpdateGameweekEliminationRequestValidator()
    {
        RuleFor(x => x.EliminationCount)
            .GreaterThanOrEqualTo(0).WithMessage("Elimination count cannot be negative")
            .LessThanOrEqualTo(100).WithMessage("Elimination count cannot exceed 100");
    }
}

public class ProcessEliminationsRequestValidator : AbstractValidator<ProcessEliminationsRequest>
{
    public ProcessEliminationsRequestValidator()
    {
        RuleFor(x => x.SeasonId)
            .NotEmpty().WithMessage("Season ID is required");

        RuleFor(x => x.GameweekNumber)
            .GreaterThan(0).WithMessage("Gameweek number must be greater than 0")
            .LessThanOrEqualTo(38).WithMessage("Gameweek number cannot exceed 38");
    }
}
