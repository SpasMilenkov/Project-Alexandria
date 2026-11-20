using Alexandria.Data.Models;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Auth.Native;

public sealed class NativeTokenRequestValidator : Validator<NativeTokenRequest>
{
    public NativeTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required")
            .MaximumLength(ValidationConstants.StringLengths.ExtraLongString);
    }
}