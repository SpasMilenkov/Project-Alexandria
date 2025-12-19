using Common;
using Common.Services;
using FastEndpoints;

namespace API.Features.Auth.Logout;

public class LogoutEndpoint(IAuthService authService) : EndpointWithoutRequest
{

    public override void Configure()
    {
        Post("/auth/logout");
        Version(0);

    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (HttpContext.Request.Cookies.TryGetValue("refresh_token", out var refreshToken))
        {
            await authService.RevokeRefreshTokenAsync(refreshToken, ct);
        }

        HttpContext.Response.Cookies.Delete("access_token");
        HttpContext.Response.Cookies.Delete("refresh_token");
        HttpContext.Response.Cookies.Delete("csrf_token");

        await Send.OkAsync(cancellation: ct);
    }
}