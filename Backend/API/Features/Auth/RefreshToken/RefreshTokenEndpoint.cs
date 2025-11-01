using Common;
using FastEndpoints;

namespace API.Features.Auth.RefreshToken;

public class RefreshTokenEndpoint(IAuthService authService, CsrfService csrfService)
    : EndpointWithoutRequest<RefreshResponse>
{
    public override void Configure()
    {
        Post("/api/auth/refresh");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!HttpContext.Request.Cookies.TryGetValue("refresh_token", out var refreshToken))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var result = await authService.RefreshTokenAsync(refreshToken, ct);
        
        if (!result.Succeeded || result.User is null)
        {
            ClearAuthCookies();
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var userId = result.User.Id.ToString();
        
        var newAccessToken = authService.GenerateAccessToken(result.User);
        var newRefreshToken = await authService.GenerateRefreshTokenAsync(result.User, ct);
        
        var (csrfCookie, csrfHeader) = csrfService.RegenerateToken(userId);

        HttpContext.Response.Cookies.Append("access_token", newAccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromMinutes(15)
        });

        HttpContext.Response.Cookies.Append("refresh_token", newRefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromDays(7),
            Path = "/api/auth/refresh"
        });

        HttpContext.Response.Cookies.Append("csrf_token", csrfCookie, new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromHours(1)
        });

        await Send.OkAsync(new RefreshResponse
        {
            Success = true,
            CsrfToken = csrfHeader
        }, ct);
    }

    private void ClearAuthCookies()
    {
        HttpContext.Response.Cookies.Delete("access_token");
        HttpContext.Response.Cookies.Delete("refresh_token");
        HttpContext.Response.Cookies.Delete("csrf_token");
    }
}

