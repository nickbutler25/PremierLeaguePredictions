using FluentValidation;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Application.Validators;

public class CreatePickRequestValidator : AbstractValidator<CreatePickRequest>
{
    public CreatePickRequestValidator(IUnitOfWork unitOfWork)
    {
        RuleFor(x => x.SeasonId)
            .NotEmpty().WithMessage("Season ID is required");

        RuleFor(x => x.GameweekNumber)
            .GreaterThan(0).WithMessage("Gameweek number must be greater than 0");

        RuleFor(x => x.TeamId)
            .NotEmpty().WithMessage("Team ID is required");

        // Async validation for deadline
        RuleFor(x => x)
            .MustAsync(async (request, cancellationToken) =>
            {
                var gameweek = await unitOfWork.Gameweeks.FirstOrDefaultAsync(
                    g => g.SeasonId == request.SeasonId && g.WeekNumber == request.GameweekNumber,
                    cancellationToken);

                if (gameweek == null)
                {
                    return true; // Let service layer handle missing gameweek
                }

                return gameweek.Deadline >= DateTime.UtcNow;
            })
            .WithMessage("The gameweek deadline has passed. Picks cannot be submitted after the deadline.");
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
