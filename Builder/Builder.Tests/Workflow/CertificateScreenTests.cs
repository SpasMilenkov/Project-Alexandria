using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AwesomeAssertions;
using Builder.Models;
using Builder.Services;
using Builder.Tests.Support;
using Builder.UI.Shell.Screens;

namespace Builder.Tests.Workflow;

[Collection(nameof(StaticConsoleTests))]
public class CertificateScreenTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "builder-certificate-screen-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Run_generatedChoice_validatesWithoutWritingCertificateFiles()
    {
        using var harness = new ShellHarness(new FakeKeys().Enter());
        harness.Context.InstallPath = Path.Combine(_root, "installation");

        CertificateScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Continue);

        harness.Context.Config.Certificates.Mode.Should().Be(CertificateSettings.GeneratedMode);
        Directory.Exists(harness.Context.InstallPath).Should().BeFalse();
    }

    [Fact]
    public void Run_importChoice_validatesPairAndStoresPathsWithoutCopyingSecrets()
    {
        var settings = CreateImport();
        using var harness = new ShellHarness(new FakeKeys().Down().Enter()
            .Text(settings.ImportCertificatePath).Enter().Text(settings.ImportPrivateKeyPath).Enter());
        harness.Context.InstallPath = Path.Combine(_root, "installation");

        CertificateScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Continue);

        harness.Context.Config.Certificates.Mode.Should().Be(CertificateSettings.ImportedMode);
        harness.Context.Config.Certificates.ImportCertificatePath.Should().Be(settings.ImportCertificatePath);
        harness.Context.Config.Certificates.ImportPrivateKeyPath.Should().Be(settings.ImportPrivateKeyPath);
        Directory.Exists(harness.Context.InstallPath).Should().BeFalse();
        harness.Output.Should().NotContain("BEGIN PRIVATE KEY");
    }

    [Fact]
    public void Run_savedImportWithoutOriginalSources_reusesInstalledPair()
    {
        var settings = CreateImport();
        using var harness = new ShellHarness(new FakeKeys().Enter());
        harness.Context.InstallPath = Path.Combine(_root, "installation");
        harness.Context.Config.Certificates = settings;
        var installed = new CertificateService().EnsureCertificates(harness.Context.InstallPath, "192.0.2.10", settings);
        File.Delete(settings.ImportCertificatePath);
        File.Delete(settings.ImportPrivateKeyPath);

        CertificateScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Continue);

        new CertificateService().ReadDetails(harness.Context.InstallPath, "192.0.2.10", settings)
            .CertificateSha256Fingerprint.Should().Be(installed.CertificateSha256Fingerprint);
        harness.Output.Should().Contain("Keep the saved certificate settings");
        File.Exists(settings.ImportCertificatePath).Should().BeFalse();
    }

    private CertificateSettings CreateImport()
    {
        Directory.CreateDirectory(_root);
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(IPAddress.Parse("192.0.2.10"));
        names.AddDnsName("localhost");
        request.CertificateExtensions.Add(names.Build());
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(365));
        var settings = new CertificateSettings
        {
            Mode = CertificateSettings.ImportedMode,
            ImportCertificatePath = Path.Combine(_root, "source.crt"),
            ImportPrivateKeyPath = Path.Combine(_root, "source.key"),
        };
        ProtectedFile.WriteAtomic(settings.ImportCertificatePath, certificate.ExportCertificatePem());
        ProtectedFile.WriteAtomic(settings.ImportPrivateKeyPath, key.ExportPkcs8PrivateKeyPem());

        return settings;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
