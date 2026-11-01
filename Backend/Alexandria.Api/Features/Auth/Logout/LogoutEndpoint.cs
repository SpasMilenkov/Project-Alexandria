using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Auth.Logout;

public class LogoutEndpoint(IAuthService authService) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/auth/logout");
        Version(0);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (HttpContext.Request.Cookies.TryGetValue(AuthCookies.RefreshToken, out var refreshToken))
        {
            await authService.RevokeRefreshTokenAsync(refreshToken, ct);
        }

        HttpContext.Response.ClearSessionCookies();

        await Send.OkAsync(cancellation: ct);
    }
}