using FluentValidation;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Core.Constants;

namespace PremierLeaguePredictions.Application.Validators;

public class GoogleLoginRequestValidator : AbstractValidator<GoogleLoginRequest>
{
    public GoogleLoginRequestValidator()
    {
        RuleFor(x => x.GoogleToken)
            .NotEmpty().WithMessage("Google token is required");
    }
}

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(ValidationRules.MaxEmailLength).WithMessage($"Email must not exceed {ValidationRules.MaxEmailLength} characters");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MaximumLength(ValidationRules.MaxFirstNameLength).WithMessage($"First name must not exceed {ValidationRules.MaxFirstNameLength} characters");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MaximumLength(ValidationRules.MaxLastNameLength).WithMessage($"Last name must not exceed {ValidationRules.MaxLastNameLength} characters");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Passwords do not match");
    }
}

public class PasswordLoginRequestValidator : AbstractValidator<PasswordLoginRequest>
{
    public PasswordLoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required");
    }
}
