using FluentValidation;
using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Validators;

public class CreateFixtureRequestValidator : AbstractValidator<CreateFixtureRequest>
{
    public CreateFixtureRequestValidator()
    {
        RuleFor(x => x.SeasonId)
            .NotEmpty().WithMessage("Season ID is required");

        RuleFor(x => x.GameweekNumber)
            .GreaterThan(0).WithMessage("Gameweek number must be greater than 0");

        RuleFor(x => x.HomeTeamId)
            .NotEmpty().WithMessage("Home team ID is required");

        RuleFor(x => x.AwayTeamId)
            .NotEmpty().WithMessage("Away team ID is required")
            .NotEqual(x => x.HomeTeamId).WithMessage("Home and away teams must be different");

        RuleFor(x => x.KickoffTime)
            .NotEmpty().WithMessage("Kickoff time is required")
            .GreaterThan(DateTime.UtcNow.AddHours(-2)).WithMessage("Kickoff time cannot be too far in the past");
    }
}

public class UpdateFixtureRequestValidator : AbstractValidator<UpdateFixtureRequest>
{
    public UpdateFixtureRequestValidator()
    {
        When(x => x.Status != null, () =>
        {
            RuleFor(x => x.Status)
                .Must(status => new[] { "SCHEDULED", "IN_PLAY", "FINISHED", "POSTPONED", "CANCELLED" }.Contains(status!))
                .WithMessage("Invalid status value");
        });

        When(x => x.HomeScore.HasValue, () =>
        {
            RuleFor(x => x.HomeScore)
                .GreaterThanOrEqualTo(0).WithMessage("Home score cannot be negative");
        });

        When(x => x.AwayScore.HasValue, () =>
        {
            RuleFor(x => x.AwayScore)
                .GreaterThanOrEqualTo(0).WithMessage("Away score cannot be negative");
        });
    }
}
