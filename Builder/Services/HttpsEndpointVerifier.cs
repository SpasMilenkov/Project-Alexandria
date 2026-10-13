using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Builder.Models;

namespace Builder.Services;

public interface IHttpsEndpointVerifier
{
    void Verify(InstallationConfig config, CancellationToken cancellationToken = default);
}

public sealed class HttpsEndpointVerifier : IHttpsEndpointVerifier
{
    private readonly Func<InstallationConfig, CookieContainer, HttpMessageHandler> _createHandler;

    public HttpsEndpointVerifier() : this(CreateHandler)
    {
    }

    internal HttpsEndpointVerifier(Func<InstallationConfig, CookieContainer, HttpMessageHandler> createHandler)
    {
        _createHandler = createHandler;
    }

    public void Verify(InstallationConfig config, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var handler = _createHandler(config, new CookieContainer());
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };

        foreach (var path in new[] { "/", "/api/status" })
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, config.PublicEndpoint.Origin + path);
                using var response = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).GetAwaiter().GetResult();

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new InvalidOperationException($"HTTPS access to {path} returned HTTP {(int)response.StatusCode}. Inspect the proxy and API logs before retrying.");
                }
            }
            catch (Exception exception) when (exception is HttpRequestException || exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException($"Alexandria's selected HTTPS address {config.PublicEndpoint.Origin} could not be verified. " +
                    "Services were preserved. Check the LAN address, DNS resolution, selected port, and certificate; see nginx/CLIENT-TRUST.txt for client and firewall checks.");
            }
        }
    }

    internal static SocketsHttpHandler CreateHandler(InstallationConfig config, CookieContainer cookies)
    {
        new CertificateService().ReadDetails(config.InstallPath, config.PublicEndpoint.Host, config.Certificates);
        using var expected = X509Certificate2.CreateFromPem(File.ReadAllText(Path.Combine(config.InstallPath, "nginx/certs/server.crt")));
        var fingerprint = SHA256.HashData(expected.RawData);
        var notBefore = expected.NotBefore.ToUniversalTime();
        var notAfter = expected.NotAfter.ToUniversalTime();

        return new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            UseCookies = true,
            CookieContainer = cookies,
            SslOptions = new SslClientAuthenticationOptions
            {
                CertificateChainPolicy = new X509ChainPolicy
                {
                    DisableCertificateDownloads = true,
                    RevocationMode = X509RevocationMode.NoCheck,
                },
                RemoteCertificateValidationCallback = (_, certificate, _, errors) =>
                    CertificateMatches(certificate, errors, fingerprint, notBefore, notAfter),
            },
        };
    }

    internal static bool CertificateMatches(X509Certificate? actual, SslPolicyErrors errors, X509Certificate2 expected)
        => CertificateMatches(actual, errors, SHA256.HashData(expected.RawData), expected.NotBefore.ToUniversalTime(), expected.NotAfter.ToUniversalTime());

    private static bool CertificateMatches(X509Certificate? actual, SslPolicyErrors errors, byte[] fingerprint, DateTime notBefore, DateTime notAfter)
    {
        if (actual is null || (errors & (SslPolicyErrors.RemoteCertificateNotAvailable | SslPolicyErrors.RemoteCertificateNameMismatch)) != 0)
        {
            return false;
        }

        var now = DateTime.UtcNow;

        if (notBefore > now || notAfter <= now) return false;

        // This handler trusts only the exact reviewed installation leaf; client trust stores stay untouched.
        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(actual.GetRawCertData()), fingerprint);
    }
}
