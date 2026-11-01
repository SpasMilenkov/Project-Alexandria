using Alexandria.Api.Features.Auth.Extensions;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;

namespace Alexandria.Tests.Unit.Auth;

public class AuthCookiesTests
{
    [Fact]
    public void AppendSessionCookies_scopesRefreshCookieToAuthRoutes_soLogoutReceivesIt()
    {
        var response = new DefaultHttpContext().Response;

        response.AppendSessionCookies("access", "refresh");

        var refreshCookie = SetCookies(response).Single(c => c.StartsWith("refresh_token=refresh"));
        HasPath(refreshCookie, AuthCookies.RefreshTokenPath).Should().BeTrue();
        refreshCookie.Should().Contain("httponly");
    }

    [Fact]
    public void AppendSessionCookies_expiresLegacyRefreshCookie()
    {
        var response = new DefaultHttpContext().Response;

        response.AppendSessionCookies("access", "refresh");

        SetCookies(response).Should().Contain(c =>
            c.StartsWith("refresh_token=;") && HasPath(c, AuthCookies.LegacyRefreshTokenPath));
    }

    [Fact]
    public void ClearSessionCookies_expiresRefreshCookieAtCurrentAndLegacyPaths()
    {
        var response = new DefaultHttpContext().Response;

        response.ClearSessionCookies();

        var refreshDeletes = SetCookies(response).Where(c => c.StartsWith("refresh_token=;")).ToList();
        refreshDeletes.Should().Contain(c => HasPath(c, AuthCookies.RefreshTokenPath));
        refreshDeletes.Should().Contain(c => HasPath(c, AuthCookies.LegacyRefreshTokenPath));
        SetCookies(response).Should().Contain(c => c.StartsWith("access_token=;"));
    }

    private static string[] SetCookies(HttpResponse response)
    {
        return response.Headers.SetCookie.Select(c => c!).ToArray();
    }

    private static bool HasPath(string setCookie, string path)
    {
        return setCookie.Split("; ").Contains($"path={path}");
    }
}
