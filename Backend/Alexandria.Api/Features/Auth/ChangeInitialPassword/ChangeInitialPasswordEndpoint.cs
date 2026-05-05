using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Auth.ChangeInitialPassword;

internal sealed class ChangeInitialPasswordRequest
{
    public required string InitialPassword { get; set; }
    public required string NewPassword { get; set; }
    public required string ConfirmPassword { get; set; }
}

internal sealed class ChangeInitialPasswordRequestValidator : Validator<ChangeInitialPasswordRequest>
{
    public ChangeInitialPasswordRequestValidator()
    {
        RuleFor(x => x.InitialPassword)
            .NotEmpty().WithMessage("Password is required");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .MaximumLength(100).WithMessage("Password must not exceed 100 characters")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches("[0-9]").WithMessage("Password must contain at least one number")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword);
    }
}

internal sealed class InitialPasswordResetEndpoint(IAuthService authService) : Endpoint<ChangeInitialPasswordRequest>
{
    public override void Configure()
    {
        Patch("/auth/change-initial-password");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(ChangeInitialPasswordRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await authService.ChangeInitialPasswordAsync(userId, req.InitialPassword, req.NewPassword, ct);

        await Send.OkAsync(cancellation: ct);
    }
}