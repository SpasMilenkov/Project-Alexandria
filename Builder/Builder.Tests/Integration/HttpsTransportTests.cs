using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Builder.Tests.Integration;

public sealed class HttpsTransportTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [DockerFact]
    [Trait("Category", "Docker")]
    public void Compose_allFeatureSelections_publishOnlySelectedHttpsPort()
    {
        using var fixture = new CachedHttpsFixture(monitoring: true);
        fixture.Prepare(requireApplication: false);
        fixture.ValidateAllSelections();
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    public void Proxy_monitoringEnabledAndDisabled_loadsGeneratedConfigWithCachedRuntime()
    {
        foreach (var monitoring in new[] { false, true })
        {
            foreach (var imported in new[] { false, true })
            {
                using var fixture = new CachedHttpsFixture(monitoring);
                fixture.Prepare(requireApplication: false, importedCertificate: imported);
                fixture.ValidateProxyWithCachedImage();
            }
        }
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    public async Task Https_customPort_servesCurrentApplicationAuthStorageEncryptionAndMonitoring()
    {
        using var fixture = new CachedHttpsFixture(monitoring: true);
        fixture.Prepare();
        fixture.Start();
        using var client = fixture.CreateClient();
        await WaitForHttp(client, "/health");
        fixture.ValidateCoreHealth();
        await VerifyScopedTrust(fixture);
        await VerifyFrontend(client);
        await VerifyCors(fixture, client);
        await VerifyUnauthorizedAssets(fixture);
        await Login(fixture, client);
        await VerifyRefresh(client);
        await VerifyImageUpload(fixture, client);
        await VerifyEncryptedFileRoundTrip(fixture, client);
        await VerifyEscapedS3Keys(fixture, client);
        await VerifyGrafana(fixture, client);
        await VerifyLogout(client);
        fixture.ValidateCoreHealth();
    }

    private async Task VerifyScopedTrust(CachedHttpsFixture fixture)
    {
        using var hostnameHandler = fixture.CreateHandler();
        using var wrongHost = new HttpClient(hostnameHandler);
        var mismatched = new UriBuilder(fixture.Origin) { Host = "192.0.2.100", Path = "/health" }.Uri;
        await Assert.ThrowsAnyAsync<HttpRequestException>(() => wrongHost.GetAsync(mismatched, _ct));

        using var untrustedHandler = fixture.CreateHandler();
        untrustedHandler.SslOptions.CertificateChainPolicy = null;
        using var untrusted = new HttpClient(untrustedHandler);
        await Assert.ThrowsAnyAsync<HttpRequestException>(() => untrusted.GetAsync(new Uri(fixture.Origin, "/health"), _ct));
    }

    private async Task VerifyCors(CachedHttpsFixture fixture, HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/files/init-upload");
        request.Headers.Add("Origin", fixture.Origin.GetLeftPart(UriPartial.Authority));
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");
        using var response = await client.SendAsync(request, _ct);
        RequireStatus(response, HttpStatusCode.NoContent, "public-origin CORS preflight");
        CachedHttpsFixture.Require(response.Headers.GetValues("Access-Control-Allow-Origin").Single() == fixture.Origin.GetLeftPart(UriPartial.Authority) &&
                response.Headers.GetValues("Access-Control-Allow-Credentials").Single() == "true",
            "Credentialed API CORS must allow exactly the selected origin, including its custom port.");

        using var forbidden = new HttpRequestMessage(HttpMethod.Options, "/api/files/init-upload");
        forbidden.Headers.Add("Origin", "https://unrelated.invalid");
        forbidden.Headers.Add("Access-Control-Request-Method", "POST");
        using var denied = await client.SendAsync(forbidden, _ct);
        CachedHttpsFixture.Require(!denied.Headers.Contains("Access-Control-Allow-Origin"), "Credentialed API CORS must reject unrelated origins.");
    }

    private async Task VerifyFrontend(HttpClient client)
    {
        using var homepage = await client.GetAsync("/", _ct);
        RequireStatus(homepage, HttpStatusCode.OK, "homepage");
        var html = await homepage.Content.ReadAsStringAsync(_ct);
        CachedHttpsFixture.Require(html.Contains("<html", StringComparison.OrdinalIgnoreCase) && html.Contains("/assets/", StringComparison.Ordinal),
            "HTTPS must serve the current production SPA bundle.");
        CachedHttpsFixture.Require(homepage.Headers.Contains("Strict-Transport-Security") && homepage.Headers.Contains("Content-Security-Policy"),
            "The public HTTPS origin must retain transport and content security headers.");

        using var deepRoute = await client.GetAsync("/dashboard/files?fixture=deep%20route", _ct);
        RequireStatus(deepRoute, HttpStatusCode.OK, "SPA deep route");
        CachedHttpsFixture.Require(await deepRoute.Content.ReadAsStringAsync(_ct) == html, "SPA deep links must resolve to the production entrypoint.");

        var asset = Regex.Match(html, "(?:src|href)=\"(?<path>/assets/[^\"]+\\.js)\"").Groups["path"].Value;
        CachedHttpsFixture.Require(asset.Length > 0, "The SPA entrypoint must reference its generated JavaScript bundle.");
        using var script = await client.GetAsync(asset, _ct);
        RequireStatus(script, HttpStatusCode.OK, "production JavaScript asset");

        using var publicStatus = await client.GetAsync("/api/status", _ct);
        RequireStatus(publicStatus, HttpStatusCode.OK, "public API");

        using var apiRedirect = await client.GetAsync("/api", _ct);
        CachedHttpsFixture.Require(apiRedirect.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.PermanentRedirect &&
                apiRedirect.Headers.Location?.ToString() == "/api/",
            "Automatic slash redirects must preserve the custom public port with a relative Location.");
    }

    private async Task VerifyUnauthorizedAssets(CachedHttpsFixture fixture)
    {
        using var anonymous = fixture.CreateClient(authenticatedCookies: false);
        var id = Guid.NewGuid().ToString();

        foreach (var path in new[] { "/preview/" + id + "/thumbnail", "/preview/" + id + "/preview", "/stream/" + id + "/manifest.mpd", "/covers/" + id })
        {
            using var response = await anonymous.GetAsync(path, _ct);
            CachedHttpsFixture.Require(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
                "Protected preview, stream, and cover routes must reject unauthenticated requests.");
            CachedHttpsFixture.Require(response.Headers.Contains("Strict-Transport-Security"), "Protected responses must retain HTTPS security headers.");
        }

        using var directCheck = await anonymous.GetAsync("/api/auth/preview-check", _ct);
        RequireStatus(directCheck, HttpStatusCode.NotFound, "internal authorization subrequest route");
    }

    private async Task Login(CachedHttpsFixture fixture, HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = fixture.Context.AdminAccount!.Email,
            password = fixture.Context.AdminAccount.Password,
        }, JsonOptions, _ct);
        RequireStatus(response, HttpStatusCode.OK, "admin sign-in");
        var cookies = response.Headers.GetValues("Set-Cookie").ToArray();
        CachedHttpsFixture.Require(cookies.Any(cookie => cookie.StartsWith("access_token=", StringComparison.Ordinal)) &&
                cookies.Any(cookie => cookie.StartsWith("refresh_token=", StringComparison.Ordinal)),
            "Sign-in must create access and refresh cookies through HTTPS.");
        CachedHttpsFixture.Require(cookies.All(cookie => cookie.Contains("secure", StringComparison.OrdinalIgnoreCase) &&
                cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase) && cookie.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase)),
            "Authentication cookies must retain Secure, HttpOnly, and SameSite behavior.");

        using var profile = await client.GetAsync("/api/users/profile", _ct);
        RequireStatus(profile, HttpStatusCode.OK, "authenticated API request");
    }

    private async Task VerifyRefresh(HttpClient client)
    {
        using var response = await client.PostAsync("/api/auth/refresh", content: null, _ct);
        RequireStatus(response, HttpStatusCode.OK, "refresh-token rotation");
        CachedHttpsFixture.Require(response.Headers.GetValues("Set-Cookie").Any(cookie =>
                cookie.StartsWith("refresh_token=", StringComparison.Ordinal) && cookie.Contains("secure", StringComparison.OrdinalIgnoreCase)),
            "Refresh must rotate its secure cookie through the selected origin.");
    }

    private async Task VerifyImageUpload(CachedHttpsFixture fixture, HttpClient client)
    {
        using var initialized = await client.PostAsync("/api/settings/appearance/background-image", content: null, _ct);
        RequireStatus(initialized, HttpStatusCode.OK, "image upload initialization");
        using var body = await ReadJson(initialized);
        var url = body.RootElement.GetProperty("uploadUrl").GetString()!;
        AssertPublicUrl(fixture, url, "alexandria-images");
        var image = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl6w2QAAAAASUVORK5CYII=");
        using var content = new ByteArrayContent(image);
        using var uploaded = await client.PutAsync(url, content, _ct);
        RequireStatus(uploaded, HttpStatusCode.OK, "presigned image upload");
    }

    private async Task VerifyEncryptedFileRoundTrip(CachedHttpsFixture fixture, HttpClient client)
    {
        const string password = "synthetic file password";
        const int iterations = 800_000;
        var plaintext = Encoding.UTF8.GetBytes("Encrypted HTTPS fixture: résumé +% #?& / 日本語\n");
        var salt = RandomNumberGenerator.GetBytes(16);
        var iv = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var ciphertext = new byte[plaintext.Length];
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);

        using (var aes = new AesGcm(key, tag.Length)) aes.Encrypt(iv, plaintext, ciphertext, tag);

        using var initialized = await client.PostAsJsonAsync("/api/files/init-upload", new
        {
            contentType = "application/octet-stream",
            hash = fixture.ContentHash(ciphertext),
            contentLength = ciphertext.Length,
        }, JsonOptions, _ct);
        RequireStatus(initialized, HttpStatusCode.OK, "encrypted file initialization");
        using var upload = await ReadJson(initialized);
        var uploadUrl = upload.RootElement.GetProperty("uploadUrl").GetString()!;
        AssertPublicUrl(fixture, uploadUrl, "alexandria-temp");

        using var content = new ByteArrayContent(ciphertext);
        content.Headers.ContentType = new("application/octet-stream");
        using var uploaded = await client.PutAsync(uploadUrl, content, _ct);
        RequireStatus(uploaded, HttpStatusCode.OK, "API-presigned encrypted file upload");

        var fileName = "transport résumé +% #?& 日本語 " + Guid.NewGuid().ToString("N") + ".bin";
        using var finalized = await client.PostAsJsonAsync("/api/files/finalize-upload", new
        {
            uploadId = upload.RootElement.GetProperty("uploadId").GetGuid(),
            fileName,
            isEncrypted = true,
            encryptionIv = iv,
            encryptionSalt = salt,
            integrityTag = tag,
            encryptionHint = "synthetic fixture",
            iterationCount = iterations,
        }, JsonOptions, _ct);
        RequireStatus(finalized, HttpStatusCode.OK, "encrypted file finalization");

        using var searched = await client.GetAsync("/api/files/search?currentPage=0&pageSize=20&nameContains=" + Uri.EscapeDataString(fileName), _ct);
        RequireStatus(searched, HttpStatusCode.OK, "uploaded file lookup");
        using var files = await ReadJson(searched);
        var items = files.RootElement.GetProperty("items").EnumerateArray().ToArray();
        CachedHttpsFixture.Require(items.Length == 1, "The encrypted file must be persisted and visible to its owner.");
        var fileId = items[0].GetProperty("fileId").GetGuid();

        using var downloadInfo = await client.GetAsync("/api/files/download/" + fileId, _ct);
        RequireStatus(downloadInfo, HttpStatusCode.OK, "API-presigned encrypted download initialization");
        using var download = await ReadJson(downloadInfo);
        var details = download.RootElement;
        var downloadUrl = details.GetProperty("presignedUrl").GetString()!;
        AssertPublicUrl(fixture, downloadUrl, "alexandria-temp", "alexandria-files");
        CachedHttpsFixture.Require(details.GetProperty("isEncrypted").GetBoolean() && details.GetProperty("iterationCount").GetInt32() == iterations,
            "Download metadata must retain encryption and PBKDF2 parameters.");

        using var downloaded = await client.GetAsync(downloadUrl, _ct);
        RequireStatus(downloaded, HttpStatusCode.OK, "API-presigned encrypted download");
        var returnedCiphertext = await downloaded.Content.ReadAsByteArrayAsync(_ct);
        var returnedIv = details.GetProperty("encryptionIv").GetBytesFromBase64();
        var returnedSalt = details.GetProperty("encryptionSalt").GetBytesFromBase64();
        var returnedTag = details.GetProperty("integrityTag").GetBytesFromBase64();
        var returnedKey = Rfc2898DeriveBytes.Pbkdf2(password, returnedSalt, details.GetProperty("iterationCount").GetInt32(), HashAlgorithmName.SHA256, 32);
        var decrypted = new byte[returnedCiphertext.Length];

        using (var aes = new AesGcm(returnedKey, returnedTag.Length)) aes.Decrypt(returnedIv, returnedCiphertext, returnedTag, decrypted);

        CachedHttpsFixture.Require(plaintext.AsSpan().SequenceEqual(decrypted), "Encrypted file bytes must round-trip using the frontend's AES-GCM and PBKDF2 protocol.");
    }

    private async Task VerifyEscapedS3Keys(CachedHttpsFixture fixture, HttpClient client)
    {
        var values = fixture.Context.Config.EnvironmentValues;
        var objectKey = "transport/space + plus% percent #hash ?query &and/日本語.bin";
        var payload = Encoding.UTF8.GetBytes("signed escape round trip");
        var put = S3PresignedRequest.Sign(fixture.Origin, "alexandria-temp", objectKey, "PUT", values["GARAGE_S3_ACCESS_KEY"], values["GARAGE_S3_SECRET_KEY"]);
        using var content = new ByteArrayContent(payload);
        using var uploaded = await client.PutAsync(put, content, _ct);
        RequireStatus(uploaded, HttpStatusCode.OK, "escaped-key signed upload");

        var get = S3PresignedRequest.Sign(fixture.Origin, "alexandria-temp", objectKey, "GET", values["GARAGE_S3_ACCESS_KEY"], values["GARAGE_S3_SECRET_KEY"],
            new Dictionary<string, string> { ["response-content-disposition"] = "attachment; filename=\"résumé +% #?&.bin\"" });
        using var downloaded = await client.GetAsync(get, _ct);
        RequireStatus(downloaded, HttpStatusCode.OK, "escaped-key and query signed download");
        var returned = await downloaded.Content.ReadAsByteArrayAsync(_ct);
        CachedHttpsFixture.Require(payload.AsSpan().SequenceEqual(returned),
            "The proxy must preserve signed object-key and query escaping.");

        using var unsigned = await client.GetAsync(get.GetLeftPart(UriPartial.Path), _ct);
        RequireStatus(unsigned, HttpStatusCode.Forbidden, "unsigned private object access");
        var tampered = get.AbsoluteUri[..^1] + (get.AbsoluteUri[^1] == '0' ? "1" : "0");
        using var rejected = await client.GetAsync(tampered, _ct);
        RequireStatus(rejected, HttpStatusCode.Forbidden, "tampered presigned signature");
    }

    private async Task VerifyGrafana(CachedHttpsFixture fixture, HttpClient client)
    {
        using var redirect = await client.GetAsync("/monitoring?fixture=space%20plus%2B", _ct);
        RequireStatus(redirect, HttpStatusCode.PermanentRedirect, "Grafana trailing-slash redirect");
        CachedHttpsFixture.Require(redirect.Headers.Location?.AbsoluteUri == fixture.Origin.GetLeftPart(UriPartial.Authority) + "/monitoring/?fixture=space%20plus%2B",
            "Monitoring redirects must preserve the selected origin, custom port, and escaped query.");

        await WaitForHttp(client, "/monitoring/login");
        using var loginPage = await client.GetAsync("/monitoring/login", _ct);
        RequireStatus(loginPage, HttpStatusCode.OK, "Grafana login page");
        var html = await loginPage.Content.ReadAsStringAsync(_ct);
        var asset = Regex.Match(html, "(?:src|href)=\"(?<path>(?:/monitoring/)?public/[^\"]+\\.(?:js|css))\"").Groups["path"].Value;
        CachedHttpsFixture.Require(asset.Length > 0, "Grafana must serve assets under its configured subpath.");

        if (!asset.StartsWith('/')) asset = "/monitoring/" + asset;

        using var assetResponse = await client.GetAsync(asset, _ct);
        RequireStatus(assetResponse, HttpStatusCode.OK, "Grafana subpath asset");
        using var unauthorized = await client.GetAsync("/monitoring/api/user", _ct);
        RequireStatus(unauthorized, HttpStatusCode.Unauthorized, "unauthenticated Grafana API");
        using var unauthorizedMetrics = await client.GetAsync("/monitoring/api/datasources/proxy/uid/alexandria-prometheus/api/v1/query?query=up", _ct);
        RequireStatus(unauthorizedMetrics, HttpStatusCode.Unauthorized, "unauthenticated monitoring datasource");

        using var authenticated = await client.PostAsJsonAsync("/monitoring/login", new
        {
            user = fixture.Context.Config.EnvironmentValues["GRAFANA_USER"],
            password = fixture.Context.Config.EnvironmentValues["GRAFANA_PASSWORD"],
        }, JsonOptions, _ct);
        RequireStatus(authenticated, HttpStatusCode.OK, "Grafana sign-in under shared HTTPS origin");
        var session = authenticated.Headers.GetValues("Set-Cookie")
            .Single(cookie => cookie.StartsWith("alexandria_grafana_session=", StringComparison.Ordinal));
        CachedHttpsFixture.Require(session.Contains("Path=/monitoring", StringComparison.OrdinalIgnoreCase) &&
                session.Contains("secure", StringComparison.OrdinalIgnoreCase) &&
                session.Contains("httponly", StringComparison.OrdinalIgnoreCase) &&
                session.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase),
            "Grafana must scope its distinct secure session cookie to the monitoring path.");

        using var user = await client.GetAsync("/monitoring/api/user", _ct);
        RequireStatus(user, HttpStatusCode.OK, "authenticated Grafana API");
        await VerifyMonitoringProvisioning(client);

        using var invoker = new HttpMessageInvoker(fixture.CreateHandler());
        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Origin", fixture.Origin.GetLeftPart(UriPartial.Authority));
        var websocketUri = new UriBuilder(new Uri(fixture.Origin, "/monitoring/api/live/ws")) { Scheme = "wss" }.Uri;
        await socket.ConnectAsync(websocketUri, invoker, _ct);
        CachedHttpsFixture.Require(socket.State == WebSocketState.Open, "Authenticated Grafana live connections must upgrade through the shared HTTPS port.");
        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "fixture complete", _ct);
    }

    private async Task VerifyMonitoringProvisioning(HttpClient client)
    {
        foreach (var uid in new[] { "alexandria-overview", "alexandria-api", "alexandria-logs" })
        {
            using var response = await client.GetAsync("/monitoring/api/dashboards/uid/" + uid, _ct);
            RequireStatus(response, HttpStatusCode.OK, "provisioned dashboard over HTTPS");
            using var dashboard = await ReadJson(response);
            CachedHttpsFixture.Require(dashboard.RootElement.GetProperty("dashboard").GetProperty("panels").GetArrayLength() > 0,
                "Each provisioned dashboard must contain panels through the authenticated proxy.");
        }

        foreach (var (uid, endpoint, query) in new[]
        {
            ("alexandria-prometheus", "api/v1/query", "up"),
            ("alexandria-loki", "loki/api/v1/query_range", "{service=\"api\"}"),
        })
        {
            var deadline = DateTimeOffset.UtcNow.AddMinutes(2);
            var populated = false;

            while (DateTimeOffset.UtcNow < deadline)
            {
                using var response = await client.GetAsync("/monitoring/api/datasources/proxy/uid/" + uid + "/" + endpoint +
                    "?query=" + Uri.EscapeDataString(query), _ct);
                RequireStatus(response, HttpStatusCode.OK, "live datasource query over HTTPS");
                using var document = await ReadJson(response);
                CachedHttpsFixture.Require(document.RootElement.GetProperty("status").GetString() == "success",
                    "Grafana must successfully query its provisioned datasource over HTTPS.");
                var results = document.RootElement.GetProperty("data").GetProperty("result").EnumerateArray().ToArray();
                populated = results.Length > 0;

                if (uid == "alexandria-prometheus")
                    populated = results.Length == 8 && results.All(result => result.GetProperty("value")[1].GetString() == "1");

                if (populated) break;

                await Task.Delay(TimeSpan.FromSeconds(1), _ct);
            }

            CachedHttpsFixture.Require(populated, "The provisioned datasource must expose actual healthy metrics or API logs through HTTPS: " + uid);
        }
    }

    private async Task VerifyLogout(HttpClient client)
    {
        using var logout = await client.PostAsync("/api/auth/logout", content: null, _ct);
        RequireStatus(logout, HttpStatusCode.OK, "sign-out");
        using var profile = await client.GetAsync("/api/users/profile", _ct);
        RequireStatus(profile, HttpStatusCode.Unauthorized, "authenticated API after sign-out");
        using var refresh = await client.PostAsync("/api/auth/refresh", content: null, _ct);
        RequireStatus(refresh, HttpStatusCode.Unauthorized, "refresh after sign-out");
    }

    private async Task WaitForHttp(HttpClient client, string path)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using var response = await client.GetAsync(path, _ct);

                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), _ct);
        }

        throw new InvalidOperationException("The disposable HTTPS service did not become ready: " + path);
    }

    private async Task<JsonDocument> ReadJson(HttpResponseMessage response) => JsonDocument.Parse(await response.Content.ReadAsStringAsync(_ct));

    private static void AssertPublicUrl(CachedHttpsFixture fixture, string value, params string[] buckets)
    {
        var uri = new Uri(value);
        CachedHttpsFixture.Require(uri.GetLeftPart(UriPartial.Authority) == fixture.Origin.GetLeftPart(UriPartial.Authority),
            "API-presigned URLs must retain the selected HTTPS host and custom port.");
        CachedHttpsFixture.Require(buckets.Any(bucket => uri.AbsolutePath.StartsWith('/' + bucket + '/', StringComparison.Ordinal)),
            "API-presigned URLs must retain their expected bucket prefix.");
        CachedHttpsFixture.Require(uri.Query.Contains("X-Amz-Signature=", StringComparison.Ordinal),
            "API-presigned URLs must retain their signature.");
    }

    private static void RequireStatus(HttpResponseMessage response, HttpStatusCode expected, string operation) =>
        CachedHttpsFixture.Require(response.StatusCode == expected, $"Unexpected response for {operation}: {(int)response.StatusCode}; expected {(int)expected}.");
}
