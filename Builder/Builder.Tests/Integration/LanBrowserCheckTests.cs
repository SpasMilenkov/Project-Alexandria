using System.Globalization;
using System.Text.Json;
using Builder.Models;
using Builder.Services;

namespace Builder.Tests.Integration;

// This explicit manual check keeps an isolated stack available while a person uses LAN browsers.
public sealed class LanBrowserCheckTests
{
    [Fact(Skip = "Manual LAN check requires explicit opt-in.", SkipUnless = nameof(Enabled))]
    [Trait("Category", "ManualLan")]
    public async Task Lan_selectedAddress_holdsIsolatedPreviewUntilReleased()
    {
        var host = Required("ALEXANDRIA_BUILDER_LAN_HOST");
        var port = int.Parse(Required("ALEXANDRIA_BUILDER_LAN_PORT"), CultureInfo.InvariantCulture);
        var sessionPath = Path.GetFullPath(Required("ALEXANDRIA_BUILDER_LAN_SESSION"));
        CachedHttpsFixture.Require(!File.Exists(sessionPath) && !Directory.Exists(sessionPath),
            "Choose a new session file; existing files are preserved.");
        var certificatePath = Environment.GetEnvironmentVariable("ALEXANDRIA_BUILDER_LAN_CERTIFICATE");
        CertificateSettings? certificates = null;

        if (!string.IsNullOrWhiteSpace(certificatePath))
        {
            certificates = new CertificateSettings
            {
                Mode = CertificateSettings.ImportedMode,
                ImportCertificatePath = Path.GetFullPath(certificatePath),
                ImportPrivateKeyPath = Path.GetFullPath(Required("ALEXANDRIA_BUILDER_LAN_PRIVATE_KEY")),
            };
        }

        var endpoint = new PublicEndpoint { Host = host, HttpsPort = port };
        endpoint.Validate();
        CachedHttpsFixture.Require(new SystemChecker().IsPortAvailable(port), "The selected LAN preview port is occupied.");
        var bindAddress = Environment.GetEnvironmentVariable("ALEXANDRIA_BUILDER_LAN_BIND_ADDRESS");
        using var fixture = new CachedHttpsFixture(monitoring: true, lanEndpoint: endpoint, certificates: certificates,
            lanBindAddress: bindAddress);
        fixture.Prepare();
        fixture.Start();
        fixture.ValidateCoreHealth();
        var ct = TestContext.Current.CancellationToken;
        new HttpsEndpointVerifier().Verify(fixture.Context.Config, ct);
        var trust = new CertificateService().ReadDetails(fixture.Target, host, fixture.Context.Config.Certificates);
        var releasePath = Path.Combine(fixture.Target, "finish-browser-check");
        ProtectedFile.WriteAtomic(sessionPath, JsonSerializer.Serialize(new
        {
            Origin = endpoint.Origin,
            Target = fixture.Target,
            Project = fixture.Context.Config.ProjectName,
            AdminEmail = fixture.Context.AdminAccount!.Email,
            AdminPassword = fixture.Context.AdminAccount.Password,
            GrafanaUser = fixture.Context.Config.EnvironmentValues["GRAFANA_USER"],
            GrafanaPassword = fixture.Context.Config.EnvironmentValues["GRAFANA_PASSWORD"],
            TrustCertificate = trust.TrustCertificatePath,
            TrustFingerprint = trust.TrustSha256Fingerprint,
            ReleasePath = releasePath,
        }, new JsonSerializerOptions { WriteIndented = true }));

        try
        {
            var deadline = DateTimeOffset.UtcNow.AddMinutes(30);

            while (!File.Exists(releasePath))
            {
                if (DateTimeOffset.UtcNow >= deadline)
                    throw new InvalidOperationException("The manual browser check timed out; its isolated resources will be removed.");

                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }
        finally
        {
            File.Delete(sessionPath);
        }
    }

    public static bool Enabled => Environment.GetEnvironmentVariable("ALEXANDRIA_BUILDER_DOCKER_TESTS") == "1" &&
        Environment.GetEnvironmentVariable("ALEXANDRIA_BUILDER_LAN_CHECK") == "1";

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException("The manual browser fixture requires " + name + ".");
}
