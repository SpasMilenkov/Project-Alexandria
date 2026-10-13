using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Builder.Models;

namespace Builder.Services;

public interface IAdminLoginVerifier
{
    void Verify(InstallationConfig config, CancellationToken cancellationToken = default);
}

public sealed class AdminLoginVerifier : IAdminLoginVerifier
{
    private readonly Func<InstallationConfig, CookieContainer, HttpMessageHandler> _createHandler;
    private readonly TimeSpan _timeout;

    public AdminLoginVerifier() : this(HttpsEndpointVerifier.CreateHandler)
    {
    }

    internal AdminLoginVerifier(Func<InstallationConfig, CookieContainer, HttpMessageHandler> createHandler, TimeSpan? timeout = null)
    {
        _createHandler = createHandler;
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    public void Verify(InstallationConfig config, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!config.EnvironmentValues.TryGetValue("ADMIN_EMAIL", out var email) || string.IsNullOrWhiteSpace(email) ||
            !config.EnvironmentValues.TryGetValue("ADMIN_PASSWORD", out var password) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Fresh administrator sign-in requires the effective administrator email and password. " +
                "Return to the account settings and resolve the entered credentials before retrying. No account password was changed.");
        }

        var origin = new Uri(config.PublicEndpoint.Origin);
        var cookies = new CookieContainer();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_timeout);

        try
        {
            using var handler = _createHandler(config, cookies);
            using var client = new HttpClient(handler)
            {
                BaseAddress = origin,
                Timeout = Timeout.InfiniteTimeSpan,
                MaxResponseContentBufferSize = 64 * 1024,
            };

            VerifyAsync(client, cookies, email, password, deadline.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("Fresh administrator sign-in verification timed out. " +
                "Inspect the proxy and API bootstrap logs, then retry. Services and account credentials were preserved.");
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Fresh administrator sign-in could not reach the selected HTTPS authentication endpoint. " +
                "Check the selected address, certificate, proxy, and API logs, then retry. Services and account credentials were preserved.");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Fresh administrator sign-in returned an invalid authentication response. " +
                "Inspect the API bootstrap logs and confirm the source and deployed API versions agree before retrying.");
        }
    }

    private static async Task VerifyAsync(HttpClient client, CookieContainer cookies, string email, string password, CancellationToken ct)
    {
        using var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login");
        loginRequest.Headers.Add("Origin", client.BaseAddress!.GetLeftPart(UriPartial.Authority));
        loginRequest.Content = CreateLoginContent(email, password);
        using var login = await client.SendAsync(loginRequest, ct);
        RequireOk(login, "sign-in");

        using var identity = JsonDocument.Parse(await login.Content.ReadAsStringAsync(ct));
        ValidateIdentity(identity.RootElement, email);
        ValidateCookies(login, cookies, client.BaseAddress);

        // The current API uses same-origin cookies and defines no CSRF challenge or header exchange.
        using var profile = await client.GetAsync("/api/users/profile", ct);
        RequireOk(profile, "authenticated profile check");
        using var authenticated = JsonDocument.Parse(await profile.Content.ReadAsStringAsync(ct));

        if (!HasEmail(authenticated.RootElement, email))
        {
            throw new InvalidOperationException("Fresh administrator sign-in did not authenticate the expected account through its session cookie. " +
                "Inspect the API authentication and administrator seeding logs before retrying. No account password was changed.");
        }
    }

    private static HttpContent CreateLoginContent(string email, string password)
    {
        using var buffer = new MemoryStream();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("email", email);
            writer.WriteString("password", password);
            writer.WriteEndObject();
        }

        var content = new ByteArrayContent(buffer.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        return content;
    }

    private static void RequireOk(HttpResponseMessage response, string operation)
    {
        if (response.StatusCode == HttpStatusCode.OK) return;

        throw new InvalidOperationException($"Fresh administrator {operation} was rejected (HTTP {(int)response.StatusCode}). " +
            "Check the administrator email and password and inspect the API bootstrap logs before retrying. No account password was changed.");
    }

    private static void ValidateIdentity(JsonElement response, string email)
    {
        if (response.ValueKind == JsonValueKind.Object &&
            response.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True &&
            response.TryGetProperty("user", out var user) && HasEmail(user, email) &&
            user.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String && Guid.TryParse(id.GetString(), out var userId) && userId != Guid.Empty &&
            response.TryGetProperty("userRoles", out var roles) && roles.ValueKind == JsonValueKind.Array &&
            roles.EnumerateArray().Any(role => role.ValueKind == JsonValueKind.String && role.GetString() == "Admin"))
        {
            return;
        }

        throw new InvalidOperationException("Fresh administrator sign-in did not establish the expected account identity and Admin role. " +
            "Check administrator seeding, account settings, and API bootstrap logs before retrying. No account password was changed.");
    }

    private static bool HasEmail(JsonElement user, string expected) =>
        user.ValueKind == JsonValueKind.Object && user.TryGetProperty("email", out var email) &&
        email.ValueKind == JsonValueKind.String && string.Equals(email.GetString(), expected, StringComparison.OrdinalIgnoreCase);

    private static void ValidateCookies(HttpResponseMessage response, CookieContainer cookies, Uri origin)
    {
        var headers = response.Headers.TryGetValues("Set-Cookie", out var values) ? values.ToArray() : [];

        foreach (var (name, path) in new[] { ("access_token", "/"), ("refresh_token", "/api/auth/refresh") })
        {
            var matching = headers.Where(header => header.StartsWith(name + "=", StringComparison.Ordinal)).ToArray();
            var issued = cookies.GetCookies(new Uri(origin, path))[name];

            if (matching.Length != 1 || issued is null || string.IsNullOrEmpty(issued.Value) || !issued.Secure || !issued.HttpOnly || issued.Path != path)
            {
                throw CookieFailure();
            }

            var attributes = matching[0].Split(';').Skip(1).Select(attribute => attribute.Trim()).ToArray();

            if (!attributes.Contains("Secure", StringComparer.OrdinalIgnoreCase) ||
                !attributes.Contains("HttpOnly", StringComparer.OrdinalIgnoreCase) ||
                !attributes.Contains("SameSite=Lax", StringComparer.OrdinalIgnoreCase))
            {
                throw CookieFailure();
            }
        }
    }

    private static InvalidOperationException CookieFailure() => new("Fresh administrator sign-in did not establish the required secure authentication cookies. " +
        "Inspect the HTTPS proxy and API cookie settings; Secure, HttpOnly, SameSite=Lax, and the authentication cookie paths must be preserved.");
}
