using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using FastEndpoints;

namespace Alexandria.Api.Features.Auth.Native;

public sealed class NativeRefreshEndpoint(IAuthService authService)
    : Endpoint<NativeTokenRequest, NativeTokenResponse>
{
    public override void Configure()
    {
        Post("/auth/native/refresh");
        AllowAnonymous();
        Tags("Auth");
        Version(0);
        Summary(s =>
        {
            s.Summary = "Rotates a native client's refresh token";
            s.Description = "Only accepts tokens from /auth/native/login. Replaying a spent token after the "
                            + "grace period signs that session out everywhere.";
        });
    }

    public override async Task HandleAsync(NativeTokenRequest req, CancellationToken ct)
    {
        if (BrowserRequests.IsFromBrowser(HttpContext.Request))
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var result = await authService.RefreshTokenAsync(req.RefreshToken, SessionClient.Native, ct);

        if (!result.Succeeded || result.User is null || result.RefreshToken is null || result.SessionId is null)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        await Send.OkAsync(new NativeTokenResponse
        {
            AccessToken = authService.GenerateAccessToken(result.User, result.SessionId.Value),
            RefreshToken = result.RefreshToken,
        }, ct);
    }
}