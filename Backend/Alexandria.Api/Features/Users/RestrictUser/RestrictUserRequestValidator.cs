using Alexandria.Api.Features.Users.RestrictUser;
using FastEndpoints;
using FluentValidation;

sealed class RestrictUserRequestValidator : Validator<RestrictUserRequest>
{
    public RestrictUserRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.LockoutEndDate)
            .NotEmpty().WithMessage("Lockout end date is required.")
            .GreaterThan(DateTime.UtcNow).WithMessage("Lockout end date must be in the future.");
    }
}