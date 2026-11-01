namespace Alexandria.Api.Features.Auth.Extensions;

public static class AuthCookies
{
    public const string AccessToken = "access_token";
    public const string RefreshToken = "refresh_token";
    public const string CsrfToken = "csrf_token";

    // Logout must receive the refresh cookie to revoke it server-side, so the path covers every auth route.
    public const string RefreshTokenPath = "/api/auth";

    // Sessions issued before the path widened still hold a cookie at this path; it is removed on the next write.
    public const string LegacyRefreshTokenPath = "/api/auth/refresh";

    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    public static void AppendSessionCookies(this HttpResponse response, string accessToken, string refreshToken)
    {
        response.Cookies.Delete(RefreshToken, new CookieOptions { Path = LegacyRefreshTokenPath });

        response.Cookies.Append(AccessToken, accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = AccessTokenLifetime
        });

        response.Cookies.Append(RefreshToken, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = RefreshTokenLifetime,
            Path = RefreshTokenPath
        });
    }

    public static void ClearSessionCookies(this HttpResponse response)
    {
        response.Cookies.Delete(AccessToken);
        response.Cookies.Delete(CsrfToken);

        // Delete drops earlier Set-Cookie headers whose path contains the given one, so the shorter path goes first.
        response.Cookies.Delete(RefreshToken, new CookieOptions { Path = RefreshTokenPath });
        response.Cookies.Delete(RefreshToken, new CookieOptions { Path = LegacyRefreshTokenPath });
    }
}
