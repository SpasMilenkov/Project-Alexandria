using System.Text.RegularExpressions;
using AwesomeAssertions;
using Builder.Services;

namespace Builder.Tests.Services;

public class NginxConfigurationRendererTests
{
    [Theory]
    [InlineData("https://alexandria.lan", "alexandria.lan")]
    [InlineData("https://192.168.1.20:8443", "192.168.1.20")]
    public void Render_validOrigin_exposesTlsAndContainerLoopbackHealthOnly(string origin, string host)
    {
        var configuration = NginxConfigurationRenderer.Render(origin, monitoring: false);
        var listeners = Regex.Matches(configuration, @"^\s*listen ([^;]+);", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .ToArray();

        listeners.Should().BeEquivalentTo(new[] { "443 ssl", "127.0.0.1:8081" });
        configuration.Should().Contain($"server_name {host};");
        configuration.Should().Contain("absolute_redirect off;");
        configuration.Should().Contain("ssl_certificate     /etc/nginx/certs/server.crt;");
        configuration.Should().Contain("ssl_certificate_key /etc/nginx/certs/server.key;");
        configuration.Should().NotContain("return 301");
        configuration.Should().NotContain("{{");
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://alexandria.lan")]
    [InlineData("https://alexandria.lan:0")]
    [InlineData("https://alexandria.lan:65536")]
    [InlineData("https://alexandria.lan/path")]
    [InlineData("https://alexandria.lan/?secret=value")]
    [InlineData("https://alexandria.lan/#fragment")]
    [InlineData("https://owner:password@alexandria.lan")]
    [InlineData("https://[::1]:8443")]
    [InlineData("https://alexandria.lan\n; listen 80;")]
    public void Render_invalidOrigin_rejectsUnsafeOrUnsupportedConfiguration(string origin)
    {
        var render = () => NginxConfigurationRenderer.Render(origin, monitoring: true);

        render.Should().Throw<ArgumentException>().WithParameterName("publicOrigin");
    }

    [Fact]
    public void Render_monitoringDisabled_omitsGrafanaResolutionAndWebsocketConfiguration()
    {
        var configuration = NginxConfigurationRenderer.Render("https://alexandria.lan:8443", monitoring: false);

        configuration.Should().NotContain("grafana");
        configuration.Should().NotContain("/monitoring");
        configuration.Should().NotContain("$connection_upgrade");
    }

    [Fact]
    public void Render_monitoringEnabled_preservesSubpathExternalPortAndGrafanaAuthentication()
    {
        var configuration = NginxConfigurationRenderer.Render("https://alexandria.lan:8443/", monitoring: true);
        var monitoring = Location(configuration, "/monitoring/");

        configuration.Should().Contain("return 308 https://alexandria.lan:8443/monitoring/$is_args$args;");
        configuration.Should().Contain("map $http_upgrade $connection_upgrade");
        monitoring.Should().Contain("proxy_pass http://grafana:3000;");
        monitoring.Should().Contain("proxy_redirect http://grafana:3000/ https://alexandria.lan:8443/;");
        monitoring.Should().Contain("proxy_set_header Host $http_host;");
        monitoring.Should().Contain("proxy_http_version 1.1;");
        monitoring.Should().Contain("proxy_set_header Upgrade $http_upgrade;");
        monitoring.Should().Contain("proxy_set_header Connection $connection_upgrade;");
        monitoring.Should().NotContain("rewrite");
        monitoring.Should().NotContain("Authorization");
        monitoring.Should().NotContain("X-WEBAUTH-USER");
        monitoring.Should().Contain("add_header Strict-Transport-Security");
        monitoring.Should().NotContain("add_header Content-Security-Policy");
    }

    [Fact]
    public void Render_signedBucketRoute_preservesOriginalUriQueryAndExternalHost()
    {
        var configuration = NginxConfigurationRenderer.Render("https://alexandria.lan:8443", monitoring: false);
        var route = Location(configuration, "~ ^/(alexandria-files|alexandria-temp)/");

        route.Should().Contain("proxy_pass http://garage:3900;");
        route.Should().Contain("proxy_set_header Host $http_host;");
        route.Should().NotContain("proxy_set_header Host $host;");
        route.Should().NotContain("rewrite");
        route.Should().NotContain("proxy_pass http://garage:3900/");
        route.Should().Contain("proxy_request_buffering off;");
    }

    [Fact]
    public void Render_onlyPresignedUploadBucketsReachTheS3Port()
    {
        var configuration = NginxConfigurationRenderer.Render("https://alexandria.lan", monitoring: false);

        Regex.Matches(configuration, @"proxy_pass http://garage:3900").Should().HaveCount(1);
        configuration.Should().NotContain("alexandria-previews/covers");
        configuration.Should().NotContain("location ~ ^/alexandria-images/");
    }

    [Fact]
    public void Render_avatars_servePublicImmutableImagesWithoutForwardingTheSession()
    {
        var configuration = NginxConfigurationRenderer.Render("https://alexandria.lan", monitoring: false);
        var route = Location(configuration,
            "~ \"^/avatars/(?<avatar_owner>[0-9a-fA-F-]{36})/(?<avatar_id>[0-9a-fA-F-]{36})$\"");

        route.Should().Contain("limit_except GET { deny all; }");
        route.Should().NotContain("auth_request");
        route.Should().Contain("proxy_set_header Cookie \"\";");
        route.Should().Contain("proxy_set_header Authorization \"\";");
        route.Should().Contain("proxy_hide_header Set-Cookie;");
        route.Should().Contain("proxy_pass http://garage:3902/avatars/$avatar_owner/$avatar_id;");
        route.Should().Contain("proxy_set_header Host alexandria-images.web.garage.localhost;");
        route.Should().Contain("immutable");
        route.Should().Contain("add_header Content-Security-Policy");
    }

    [Fact]
    public void Render_upstreamFailures_serveTheBundledErrorPage()
    {
        var configuration = NginxConfigurationRenderer.Render("https://alexandria.lan", monitoring: false);
        var page = Location(configuration, "= /502.html");

        configuration.Should().Contain("error_page 502 503 504 /502.html;");
        page.Should().Contain("root /etc/nginx/html;");
        page.Should().Contain("internal;");
    }

    [Theory]
    [InlineData("/stream/", "stream")]
    [InlineData("~ ^/preview/(?<version_id>[0-9a-fA-F-]+)/(?<kind>preview|thumbnail)$", "preview")]
    [InlineData("~ ^/covers/(?<playlist_id>[0-9a-fA-F-]+)$", "cover")]
    [InlineData("~ ^/images/(?<user_id>[0-9a-fA-F-]+)$", "image")]
    public void Render_protectedRoutes_requiresInternalCookieAuthorizationBeforeStorage(string location, string check)
    {
        var configuration = NginxConfigurationRenderer.Render("https://alexandria.lan", monitoring: false);
        var route = Location(configuration, location);
        var auth = Location(configuration, $"= /api/auth/{check}-check");

        route.Should().Contain($"auth_request /api/auth/{check}-check;");
        auth.Should().Contain("internal;");
        auth.Should().Contain("proxy_pass_request_body off;");
        auth.Should().Contain("proxy_set_header Cookie $http_cookie;");
        auth.Should().Contain("proxy_set_header X-Original-URI $request_uri;");

        if (check == "stream")
        {
            route.Should().Contain("proxy_set_header Range $http_range;");
            route.Should().Contain("proxy_set_header If-Range $http_if_range;");
            route.Should().Contain("proxy_pass_header Content-Range;");
            route.Should().Contain("proxy_buffering off;");
            route.Should().Contain("proxy_request_buffering off;");
        }
        else
        {
            route.Should().Contain("proxy_cache previews_cache;");
            route.Should().Contain("proxy_cache_lock on;");
            route.Should().Contain("add_header Cache-Control");
            route.Should().Contain("add_header Content-Security-Policy");
            route.Should().Contain("add_header Strict-Transport-Security");
            configuration.Should().Contain("resolver 127.0.0.11 valid=30s ipv6=off;");
        }
    }

    private static string Location(string configuration, string location)
    {
        var header = $"location {location} {{";
        var start = configuration.IndexOf(header, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);

        // The block opens at the header's own brace; a regex location can contain braces itself.
        var opening = start + header.Length - 1;
        var depth = 1;
        var end = opening + 1;

        while (depth > 0 && end < configuration.Length)
        {
            if (configuration[end] == '{') depth++;
            if (configuration[end] == '}') depth--;
            end++;
        }

        depth.Should().Be(0);

        return configuration[start..end];
    }
}
