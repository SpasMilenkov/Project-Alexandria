using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Users.UpdateProfile;

sealed class UpdateProfileRequestValidator : Validator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("Username is required.")
            .MinimumLength(2).WithMessage("Username must be at least 2 characters.")
            .MaximumLength(50).WithMessage("Username cannot exceed 50 characters.")
            .Matches("^[a-zA-Z0-9_.-]+$")
            .WithMessage("Username can only contain letters, numbers, underscores, dots and hyphens.");
    }
}
