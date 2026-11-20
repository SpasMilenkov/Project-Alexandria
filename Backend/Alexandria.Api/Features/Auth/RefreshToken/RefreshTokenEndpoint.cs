using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using FastEndpoints;

namespace Alexandria.Api.Features.Auth.RefreshToken;

public class RefreshTokenEndpoint(IAuthService authService)
    : EndpointWithoutRequest<RefreshResponse>
{
    public override void Configure()
    {
        Post("/auth/refresh");
        AllowAnonymous();
        Version(0);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!HttpContext.Request.Cookies.TryGetValue(AuthCookies.RefreshToken, out var refreshToken))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var result = await authService.RefreshTokenAsync(refreshToken, SessionClient.Browser, ct);

        if (!result.Succeeded || result.User is null || result.RefreshToken is null || result.SessionId is null)
        {
            HttpContext.Response.ClearSessionCookies();
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var newAccessToken = authService.GenerateAccessToken(result.User, result.SessionId.Value);

        HttpContext.Response.AppendSessionCookies(newAccessToken, result.RefreshToken);

        await Send.OkAsync(new RefreshResponse
        {
            Success = true,
        }, ct);
    }
}