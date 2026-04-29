using Alexandria.Api.Features.Users.UpdateUser;
using FastEndpoints;
using FluentValidation;

sealed class UpdateUserRequestValidator : Validator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.Payload)
            .NotNull().WithMessage("Payload is required.");

        When(x => x.Payload != null, () =>
        {
            RuleFor(x => x.Payload.UserName)
                .MaximumLength(50).WithMessage("Username cannot exceed 50 characters.")
                .Matches("^[a-zA-Z0-9_.-]*$")
                .WithMessage("Username can only contain letters, numbers, underscores, dots and hyphens.")
                .When(x => !string.IsNullOrWhiteSpace(x.Payload.UserName));

            RuleFor(x => x.Payload.Email)
                .EmailAddress().WithMessage("A valid email address is required.")
                .MaximumLength(256).WithMessage("Email cannot exceed 256 characters.")
                .When(x => !string.IsNullOrWhiteSpace(x.Payload.Email));

            RuleFor(x => x.Payload.Role)
                .IsInEnum().WithMessage("Invalid role specified.")
                .When(x => x.Payload.Role.HasValue);
        });
    }
}