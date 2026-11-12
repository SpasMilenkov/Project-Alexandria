using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Auth.Sessions;

sealed class RevokeSessionEndpoint(IAuthService authService) : Endpoint<RevokeSessionRequest>
{
    public override void Configure()
    {
        Delete("/auth/sessions/{Id}");
        Policies(Common.Auth.Policies.RequireUser);
        Summary(s =>
        {
            s.Summary = "Signs one session out by revoking its refresh token";
            s.Description = "The session's access token stays valid until it expires, at most the access token lifetime.";
        });
    }

    public override async Task HandleAsync(RevokeSessionRequest req, CancellationToken ct)
    {
        var revoked = await authService.RevokeSessionAsync(User.GetUserId(), req.Id, ct);

        if (!revoked)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
