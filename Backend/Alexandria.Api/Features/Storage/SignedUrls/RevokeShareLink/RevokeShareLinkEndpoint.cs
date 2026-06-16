using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.SignedUrls.RevokeShareLink;

internal sealed class RevokeShareLinkRequest
{
    public Guid Id { get; init; }
}

internal sealed class RevokeShareLinkRequestValidator : Validator<RevokeShareLinkRequest>
{
    public RevokeShareLinkRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Share link id is required.");
    }
}

internal sealed class RevokeShareLinkEndpoint(ISignedUrlService signedUrlService)
    : Endpoint<RevokeShareLinkRequest>
{
    public override void Configure()
    {
        Delete("/share/{Id}");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(RevokeShareLinkRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var revoked = await signedUrlService.RevokeShareLinkAsync(req.Id, userId, ct);

        if (!revoked)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}