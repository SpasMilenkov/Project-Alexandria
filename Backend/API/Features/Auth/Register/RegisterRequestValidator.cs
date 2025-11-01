using FastEndpoints;
using FluentValidation;

namespace API.Features.Auth.Register;

public class RegisterRequestValidator : Validator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit")
            .Matches(@"[\W_]").WithMessage("Password must contain at least one special character");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Password confirmation is required")
            .Equal(x => x.Password).WithMessage("Passwords do not match");

        RuleFor(x => x.UserName)
            .NotEmpty()
            .MaximumLength(50).WithMessage("Username must not exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.UserName));
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(50).WithMessage("Name must not exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.Name));
    }
}