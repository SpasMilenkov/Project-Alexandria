using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Auth.Sessions;

sealed class RevokeSessionRequestValidator : Validator<RevokeSessionRequest>
{
    public RevokeSessionRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
