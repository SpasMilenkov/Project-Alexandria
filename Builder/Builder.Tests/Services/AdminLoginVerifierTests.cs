using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Builder.Models;
using Builder.Services;

namespace Builder.Tests.Services;

public sealed class AdminLoginVerifierTests
{
    private const string Email = "admin@example.invalid";
    private const string Password = "synthetic $password \"&+ A1!";
    private const string AccessToken = "synthetic-access-token";
    private const string RefreshToken = "synthetic-refresh-token";
    private const string CsrfToken = "synthetic-csrf-token";
    private const string ValidIdentity = """
        {"success":true,"user":{"id":"0ba37d49-2b83-421d-81cf-d40e7291ee18","email":"admin@example.invalid"},"userRoles":["User","Admin"]}
        """;

    [Fact]
    public void Verify_effectiveCredentials_authenticatesAdministratorWithScopedCookies()
    {
        var config = CreateConfig();
        config.Credentials["ADMIN_PASSWORD"] = "unused draft password";
        var calls = 0;
        var verifier = CreateVerifier(async (request, ct) =>
        {
            request.RequestUri!.Scheme.Should().Be("https");
            request.RequestUri.Port.Should().Be(8443);

            if (++calls == 1)
            {
                request.Method.Should().Be(HttpMethod.Post);
                request.RequestUri.AbsolutePath.Should().Be("/api/auth/login");
                request.Headers.GetValues("Origin").Single().Should().Be(config.PublicEndpoint.Origin);
                request.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
                using var credentials = JsonDocument.Parse(await request.Content.ReadAsStringAsync(ct));
                credentials.RootElement.GetProperty("email").GetString().Should().Be(Email);
                credentials.RootElement.GetProperty("password").GetString().Should().Be(Password);

                return LoginResponse(csrf: true);
            }

            calls.Should().Be(2);
            request.Method.Should().Be(HttpMethod.Get);
            request.RequestUri.AbsolutePath.Should().Be("/api/users/profile");
            var sentCookies = request.Headers.GetValues("Cookie").Single();
            sentCookies.Should().Contain("access_token=" + AccessToken).And.Contain("csrf_token=" + CsrfToken);
            sentCookies.Should().NotContain("refresh_token");
            request.Headers.Contains("Authorization").Should().BeFalse();

            return JsonResponse("{\"email\":\"ADMIN@example.invalid\"}");
        });

        verifier.Verify(config, TestContext.Current.CancellationToken);

        calls.Should().Be(2);
    }

    [Theory]
    [InlineData("ADMIN_EMAIL", null)]
    [InlineData("ADMIN_EMAIL", " ")]
    [InlineData("ADMIN_PASSWORD", null)]
    [InlineData("ADMIN_PASSWORD", " ")]
    public void Verify_missingEffectiveCredentials_failsBeforeOpeningTransport(string key, string? value)
    {
        var config = CreateConfig();

        if (value is null) config.EnvironmentValues.Remove(key);
        else config.EnvironmentValues[key] = value;

        var verifier = new AdminLoginVerifier((_, _) => throw new Xunit.Sdk.XunitException("Transport must not open without credentials."));

        var failure = Assert.Throws<InvalidOperationException>(() => verifier.Verify(config, TestContext.Current.CancellationToken));

        failure.Message.Should().Contain("effective administrator email and password");
        AssertRedacted(failure);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Redirect)]
    public void Verify_rejectedLogin_reportsStatusWithoutServerBodyOrSecrets(HttpStatusCode status)
    {
        var verifier = CreateVerifier((_, _) => Task.FromResult(JsonResponse(Password + AccessToken + RefreshToken + CsrfToken, status)));

        var failure = Assert.Throws<InvalidOperationException>(() => verifier.Verify(CreateConfig(), TestContext.Current.CancellationToken));

        failure.Message.Should().Contain("HTTP " + (int)status).And.Contain("API bootstrap logs");
        AssertRedacted(failure);
    }

    public static TheoryData<string> InvalidIdentities => new()
    {
        """{"success":false,"user":{"id":"0ba37d49-2b83-421d-81cf-d40e7291ee18","email":"admin@example.invalid"},"userRoles":["Admin"]}""",
        """{"success":true,"user":{"id":"0ba37d49-2b83-421d-81cf-d40e7291ee18","email":"different@example.invalid"},"userRoles":["Admin"]}""",
        """{"success":true,"user":{"id":"00000000-0000-0000-0000-000000000000","email":"admin@example.invalid"},"userRoles":["Admin"]}""",
        """{"success":true,"user":{"id":"0ba37d49-2b83-421d-81cf-d40e7291ee18","email":"admin@example.invalid"},"userRoles":["User"]}""",
        """{"success":true,"user":{"id":"0ba37d49-2b83-421d-81cf-d40e7291ee18","email":"admin@example.invalid"},"userRoles":"Admin"}""",
        """{"success":true,"user":null,"userRoles":["Admin"]}""",
    };

    [Theory]
    [MemberData(nameof(InvalidIdentities))]
    public void Verify_unexpectedIdentityOrRole_failsBeforeAuthenticatedCheck(string body)
    {
        var calls = 0;
        var verifier = CreateVerifier((_, _) =>
        {
            calls++;

            return Task.FromResult(LoginResponse(body));
        });

        var failure = Assert.Throws<InvalidOperationException>(() => verifier.Verify(CreateConfig(), TestContext.Current.CancellationToken));

        calls.Should().Be(1);
        failure.Message.Should().Contain("expected account identity and Admin role");
        AssertRedacted(failure);
    }

    [Theory]
    [InlineData("access_token", "Secure")]
    [InlineData("access_token", "HttpOnly")]
    [InlineData("access_token", "SameSite=Lax")]
    [InlineData("refresh_token", "Secure")]
    [InlineData("refresh_token", "HttpOnly")]
    [InlineData("refresh_token", "SameSite=Lax")]
    public void Verify_weakenedCookieSecurity_failsWithoutExposingTokens(string name, string removedAttribute)
    {
        var verifier = CreateVerifier((_, _) =>
        {
            var response = LoginResponse();
            var cookies = response.Headers.GetValues("Set-Cookie").Select(cookie => cookie.StartsWith(name + "=", StringComparison.Ordinal)
                ? cookie.Replace("; " + removedAttribute, "", StringComparison.Ordinal)
                : cookie).ToArray();
            response.Headers.Remove("Set-Cookie");
            response.Headers.Add("Set-Cookie", cookies);

            return Task.FromResult(response);
        });

        var failure = Assert.Throws<InvalidOperationException>(() => verifier.Verify(CreateConfig(), TestContext.Current.CancellationToken));

        failure.Message.Should().Contain("required secure authentication cookies");
        AssertRedacted(failure);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("wrong-path")]
    [InlineData("expired")]
    public void Verify_unusableAuthenticationCookies_fails(string defect)
    {
        var verifier = CreateVerifier((_, _) =>
        {
            var response = LoginResponse();
            var headers = response.Headers.GetValues("Set-Cookie").ToList();
            response.Headers.Remove("Set-Cookie");

            switch (defect)
            {
                case "missing":
                    headers.RemoveAt(1);
                    break;
                case "duplicate":
                    headers.Add(headers[0]);
                    break;
                case "wrong-path":
                    headers[1] = headers[1].Replace("Path=/api/auth/refresh", "Path=/", StringComparison.Ordinal);
                    break;
                case "expired":
                    headers[0] += "; Max-Age=0";
                    break;
            }

            response.Headers.Add("Set-Cookie", headers);

            return Task.FromResult(response);
        });

        var failure = Assert.Throws<InvalidOperationException>(() => verifier.Verify(CreateConfig(), TestContext.Current.CancellationToken));

        failure.Message.Should().Contain("required secure authentication cookies");
        AssertRedacted(failure);
    }

    [Fact]
    public void Verify_wrongAuthenticatedProfile_failsEvenWhenLoginClaimsAdmin()
    {
        var calls = 0;
        var verifier = CreateVerifier((_, _) => Task.FromResult(++calls == 1
            ? LoginResponse()
            : JsonResponse("{\"email\":\"different@example.invalid\"}")));

        var failure = Assert.Throws<InvalidOperationException>(() => verifier.Verify(CreateConfig(), TestContext.Current.CancellationToken));

        failure.Message.Should().Contain("session cookie");
        AssertRedacted(failure);
    }

    [Fact]
    public void Verify_unauthenticatedProfile_failsEvenWhenLoginClaimsAdmin()
    {
        var calls = 0;
        var verifier = CreateVerifier((_, _) => Task.FromResult(++calls == 1
            ? LoginResponse()
            : JsonResponse(Password + AccessToken, HttpStatusCode.Unauthorized)));

        var failure = Assert.Throws<InvalidOperationException>(() => verifier.Verify(CreateConfig(), TestContext.Current.CancellationToken));

        failure.Message.Should().Contain("authenticated profile check").And.Contain("HTTP 401");
        AssertRedacted(failure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Verify_malformedLoginOrProfileResponse_discardsParsingDetails(bool malformedProfile)
    {
        var calls = 0;
        var verifier = CreateVerifier((_, _) => Task.FromResult(malformedProfile && ++calls == 1
            ? LoginResponse()
            : JsonResponse(Password + AccessToken + RefreshToken + CsrfToken)));

        var failure = Assert.Throws<InvalidOperationException>(() => verifier.Verify(CreateConfig(), TestContext.Current.CancellationToken));

        failure.Message.Should().Contain("invalid authentication response");
        AssertRedacted(failure);
    }

    [Fact]
    public void Verify_transportFailure_discardsSensitiveExceptionDetails()
    {
        var verifier = CreateVerifier((_, _) => throw new HttpRequestException(Password + AccessToken + RefreshToken + CsrfToken));

        var failure = Assert.Throws<InvalidOperationException>(() => verifier.Verify(CreateConfig(), TestContext.Current.CancellationToken));

        failure.Message.Should().Contain("selected HTTPS authentication endpoint");
        AssertRedacted(failure);
    }

    [Fact]
    public void Verify_timeout_stopsRequestAndReportsBoundedFailure()
    {
        var cancelled = false;
        var verifier = CreateVerifier(async (_, ct) =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }
            finally
            {
                cancelled = ct.IsCancellationRequested;
            }

            throw new Xunit.Sdk.XunitException("The request should be cancelled.");
        }, TimeSpan.FromMilliseconds(50));

        var failure = Assert.Throws<InvalidOperationException>(() => verifier.Verify(CreateConfig(), TestContext.Current.CancellationToken));

        cancelled.Should().BeTrue();
        failure.Message.Should().Contain("timed out");
        AssertRedacted(failure);
    }

    [Fact]
    public void Verify_callerCancellation_propagatesWithoutBecomingLoginFailure()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var verifier = CreateVerifier((_, ct) =>
        {
            cancellation.Cancel();
            ct.ThrowIfCancellationRequested();

            throw new Xunit.Sdk.XunitException("The request should be cancelled.");
        });

        Assert.ThrowsAny<OperationCanceledException>(() => verifier.Verify(CreateConfig(), cancellation.Token));
    }

    [Fact]
    public void Verify_alreadyCancelled_doesNotOpenTransport()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var verifier = new AdminLoginVerifier((_, _) => throw new Xunit.Sdk.XunitException("Transport must not open after cancellation."));

        Assert.ThrowsAny<OperationCanceledException>(() => verifier.Verify(CreateConfig(), cancellation.Token));
    }

    private static InstallationConfig CreateConfig() => new()
    {
        PublicEndpoint = new PublicEndpoint { Host = "alexandria.example.invalid", HttpsPort = 8443 },
        EnvironmentValues = new Dictionary<string, string>
        {
            ["ADMIN_EMAIL"] = Email,
            ["ADMIN_PASSWORD"] = Password,
        },
    };

    private static AdminLoginVerifier CreateVerifier(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond, TimeSpan? timeout = null)
        => new((_, cookies) => new CookieTransportStub(cookies, respond), timeout);

    private static HttpResponseMessage LoginResponse(string body = ValidIdentity, bool csrf = false)
    {
        var response = JsonResponse(body);
        response.Headers.Add("Set-Cookie", "access_token=" + AccessToken + "; Path=/; Secure; HttpOnly; SameSite=Lax");
        response.Headers.Add("Set-Cookie", "refresh_token=" + RefreshToken + "; Path=/api/auth/refresh; Secure; HttpOnly; SameSite=Lax");

        if (csrf) response.Headers.Add("Set-Cookie", "csrf_token=" + CsrfToken + "; Path=/; Secure; SameSite=Lax");

        return response;
    }

    private static HttpResponseMessage JsonResponse(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body) };

    private static void AssertRedacted(Exception failure)
    {
        failure.ToString().Should().NotContain(Password).And.NotContain(AccessToken).And.NotContain(RefreshToken).And.NotContain(CsrfToken);
        failure.InnerException.Should().BeNull();
    }

    private sealed class CookieTransportStub(CookieContainer cookies, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var header = cookies.GetCookieHeader(request.RequestUri!);

            if (header.Length > 0) request.Headers.Add("Cookie", header);

            var response = await respond(request, cancellationToken);

            if (response.Headers.TryGetValues("Set-Cookie", out var issued))
            {
                foreach (var cookie in issued) cookies.SetCookies(request.RequestUri!, cookie);
            }

            return response;
        }
    }
}
