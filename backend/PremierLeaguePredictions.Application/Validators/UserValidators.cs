using FluentValidation;
using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Validators;

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        When(x => !string.IsNullOrEmpty(x.FirstName), () =>
        {
            RuleFor(x => x.FirstName)
                .MaximumLength(100).WithMessage("First name must not exceed 100 characters");
        });

        When(x => !string.IsNullOrEmpty(x.LastName), () =>
        {
            RuleFor(x => x.LastName)
                .MaximumLength(100).WithMessage("Last name must not exceed 100 characters");
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
