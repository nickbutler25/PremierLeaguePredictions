using FluentValidation;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Core.Constants;

namespace PremierLeaguePredictions.Application.Validators;

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        When(x => !string.IsNullOrEmpty(x.FirstName), () =>
        {
            RuleFor(x => x.FirstName)
                .MaximumLength(ValidationRules.MaxFirstNameLength).WithMessage($"First name must not exceed {ValidationRules.MaxFirstNameLength} characters");
        });

        When(x => !string.IsNullOrEmpty(x.LastName), () =>
        {
            RuleFor(x => x.LastName)
                .MaximumLength(ValidationRules.MaxLastNameLength).WithMessage($"Last name must not exceed {ValidationRules.MaxLastNameLength} characters");
        });
    }
}

public class UpdateUserStatusRequestValidator : AbstractValidator<UpdateUserStatusRequest>
{
    public UpdateUserStatusRequestValidator()
    {
        RuleFor(x => x.IsActive)
            .NotNull().WithMessage("IsActive status is required");
    }
}

public class UpdatePaymentStatusRequestValidator : AbstractValidator<UpdatePaymentStatusRequest>
{
    public UpdatePaymentStatusRequestValidator()
    {
        RuleFor(x => x.IsPaid)
            .NotNull().WithMessage("Payment status is required");
    }
}
