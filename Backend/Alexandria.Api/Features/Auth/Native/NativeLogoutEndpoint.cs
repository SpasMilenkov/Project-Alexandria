using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Auth.Native;

public sealed class NativeLogoutEndpoint(IAuthService authService) : Endpoint<NativeTokenRequest>
{
    public override void Configure()
    {
        Post("/auth/native/logout");
        Tags("Auth");
        Version(0);
        Summary(s => s.Summary = "Signs a native client out by revoking its refresh token");
    }

    public override async Task HandleAsync(NativeTokenRequest req, CancellationToken ct)
    {
        if (BrowserRequests.IsFromBrowser(HttpContext.Request))
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        await authService.LogoutAsync(User.GetUserId(), req.RefreshToken, ct);

        await Send.OkAsync(cancellation: ct);
    }
}