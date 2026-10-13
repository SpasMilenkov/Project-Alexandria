using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AwesomeAssertions;
using Builder.Models;
using Builder.Services;

namespace Builder.Tests.Services;

public sealed class CertificateServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "builder-certificate-tests", Guid.NewGuid().ToString("N"));
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));
    private string Target => Path.Combine(_directory, "installation with spaces");
    private string Certificates => Path.Combine(Target, "nginx", "certs");

    [Fact]
    public void Validate_freshGeneratedTarget_doesNotCreateFilesOrDirectories()
    {
        new CertificateService(_time).Validate(Target, "alexandria.home", new CertificateSettings());

        Directory.Exists(_directory).Should().BeFalse();
    }

    [Theory]
    [InlineData("alexandria.home")]
    [InlineData("192.168.0.106")]
    public void EnsureCertificates_generatedPair_hasSanMatchingKeyAndVerifiedLocalCa(string host)
    {
        var result = new CertificateService(_time).EnsureCertificates(Target, host, new CertificateSettings());
        using var certificate = X509Certificate2.CreateFromPemFile(result.CertificatePath, result.PrivateKeyPath);
        using var authority = X509Certificate2.CreateFromPemFile(result.TrustCertificatePath, Path.Combine(Certificates, "local-ca.key"));
        using var chain = CreateChain(authority);

        certificate.HasPrivateKey.Should().BeTrue();
        certificate.MatchesHostname(host, allowWildcards: false, allowCommonName: false).Should().BeTrue();
        certificate.Extensions.OfType<X509BasicConstraintsExtension>().Single().CertificateAuthority.Should().BeFalse();
        certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().Single().EnhancedKeyUsages.Cast<Oid>()
            .Should().Contain(oid => oid.Value == "1.3.6.1.5.5.7.3.1");
        authority.HasPrivateKey.Should().BeTrue();
        authority.Extensions.OfType<X509BasicConstraintsExtension>().Single().CertificateAuthority.Should().BeTrue();
        chain.Build(certificate).Should().BeTrue();
        chain.ChainElements[^1].Certificate.RawData.Should().Equal(authority.RawData);
        result.CertificateSha256Fingerprint.Should().Be(certificate.GetCertHashString(HashAlgorithmName.SHA256));
        result.TrustSha256Fingerprint.Should().Be(authority.GetCertHashString(HashAlgorithmName.SHA256));
        result.ExpiresAt.Should().Be(_time.GetUtcNow().AddDays(365));
        result.IsGenerated.Should().BeTrue();
        result.Reused.Should().BeFalse();
        result.RenewalInstructions.Should().Contain("same local CA");
        Directory.GetFiles(Certificates).Should().HaveCount(4);
        File.ReadAllText(result.CertificatePath).Should().NotContain("PRIVATE KEY");
        File.ReadAllText(result.TrustCertificatePath).Should().NotContain("PRIVATE KEY");
    }

    [Fact]
    public void EnsureCertificates_privateAndPendingFiles_areRestrictedFromCreation()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var privateWrites = new List<string>();
        var sut = new CertificateService(_time, (path, content, mode) =>
        {
            ProtectedFile.WriteAtomic(path, content, mode, staging =>
            {
                if (!OperatingSystem.IsWindows())
                {
                    File.GetUnixFileMode(staging).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }

                if (path.EndsWith(".key", StringComparison.Ordinal) || path.Contains(".pending.", StringComparison.Ordinal))
                {
                    privateWrites.Add(path);
                    mode.Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
            });
        });

        sut.EnsureCertificates(Target, "192.168.0.106", new CertificateSettings());

        privateWrites.Should().HaveCount(4);
        File.GetUnixFileMode(Path.Combine(Certificates, "server.key")).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.GetUnixFileMode(Path.Combine(Certificates, "local-ca.key")).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        var publicMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
        File.GetUnixFileMode(Path.Combine(Certificates, "server.crt")).Should().Be(publicMode);
        File.GetUnixFileMode(Path.Combine(Certificates, "local-ca.crt")).Should().Be(publicMode);
    }

    [Fact]
    public void EnsureCertificates_validRerun_preservesAllCertificateAndKeyBytes()
    {
        var sut = new CertificateService(_time);
        sut.EnsureCertificates(Target, "alexandria.home", new CertificateSettings());
        var original = Snapshot();

        sut.Validate(Target, "alexandria.home", new CertificateSettings());
        var result = sut.EnsureCertificates(Target, "alexandria.home", new CertificateSettings());

        result.Reused.Should().BeTrue();
        Snapshot().Should().BeEquivalentTo(original);
    }

    [Fact]
    public void ReadDetails_installedCertificate_returnsTrustDetailsWithoutRewritingFiles()
    {
        var sut = new CertificateService(_time);
        var installed = sut.EnsureCertificates(Target, "alexandria.home", new CertificateSettings());
        var original = Snapshot();
        var readOnly = new CertificateService(_time, (_, _, _) => throw new InvalidOperationException("Read-only operation attempted a write"));

        var details = readOnly.ReadDetails(Target, "alexandria.home", new CertificateSettings());

        details.CertificateSha256Fingerprint.Should().Be(installed.CertificateSha256Fingerprint);
        details.TrustSha256Fingerprint.Should().Be(installed.TrustSha256Fingerprint);
        Snapshot().Should().BeEquivalentTo(original);
    }

    [Fact]
    public void EnsureCertificates_explicitHostChange_reissuesLeafUnderSameTrustIdentity()
    {
        var sut = new CertificateService(_time);
        var first = sut.EnsureCertificates(Target, "192.168.0.106", new CertificateSettings());
        var originalAuthorityKey = File.ReadAllText(Path.Combine(Certificates, "local-ca.key"));

        sut.Validate(Target, "alexandria.home", new CertificateSettings());
        var second = sut.EnsureCertificates(Target, "alexandria.home", new CertificateSettings());
        using var certificate = X509Certificate2.CreateFromPem(File.ReadAllText(second.CertificatePath));

        second.TrustSha256Fingerprint.Should().Be(first.TrustSha256Fingerprint);
        second.CertificateSha256Fingerprint.Should().NotBe(first.CertificateSha256Fingerprint);
        File.ReadAllText(Path.Combine(Certificates, "local-ca.key")).Should().Be(originalAuthorityKey);
        certificate.MatchesHostname("alexandria.home", allowCommonName: false).Should().BeTrue();
    }

    [Fact]
    public void EnsureCertificates_expiredGeneratedLeaf_renewsWithoutReplacingCa()
    {
        var sut = new CertificateService(_time);
        var first = sut.EnsureCertificates(Target, "alexandria.home", new CertificateSettings());
        _time.Now = _time.Now.AddDays(366);

        var second = sut.EnsureCertificates(Target, "alexandria.home", new CertificateSettings());

        second.TrustSha256Fingerprint.Should().Be(first.TrustSha256Fingerprint);
        second.CertificateSha256Fingerprint.Should().NotBe(first.CertificateSha256Fingerprint);
        second.ExpiresAt.Should().Be(_time.Now.AddDays(365));
    }

    [Fact]
    public void EnsureCertificates_generatedLeafWithinRenewalWindow_renewsBeforeExpiryUnderSameCa()
    {
        var sut = new CertificateService(_time);
        var first = sut.EnsureCertificates(Target, "alexandria.home", new CertificateSettings());
        _time.Now = _time.Now.AddDays(340);

        var second = sut.EnsureCertificates(Target, "alexandria.home", new CertificateSettings());

        second.TrustSha256Fingerprint.Should().Be(first.TrustSha256Fingerprint);
        second.CertificateSha256Fingerprint.Should().NotBe(first.CertificateSha256Fingerprint);
        second.ExpiresAt.Should().Be(_time.Now.AddDays(365));
    }

    [Fact]
    public void EnsureCertificates_leafExpiryAlreadyCappedAtCaExpiry_doesNotRepeatedlyReplaceValidLeaf()
    {
        var sut = new CertificateService(_time);
        sut.EnsureCertificates(Target, "alexandria.home", new CertificateSettings());
        _time.Now = _time.Now.AddYears(10).AddDays(-40);
        var capped = sut.EnsureCertificates(Target, "alexandria.home", new CertificateSettings());
        _time.Now = _time.Now.AddDays(15);

        var reused = sut.EnsureCertificates(Target, "alexandria.home", new CertificateSettings());

        reused.Reused.Should().BeTrue();
        reused.CertificateSha256Fingerprint.Should().Be(capped.CertificateSha256Fingerprint);
        reused.TrustSha256Fingerprint.Should().Be(capped.TrustSha256Fingerprint);
    }

    [Fact]
    public void EnsureCertificates_expiredCa_preservesFilesAndRequiresExplicitTrustReplacement()
    {
        var sut = new CertificateService(_time);
        sut.EnsureCertificates(Target, "alexandria.home", new CertificateSettings());
        var original = Snapshot();
        _time.Now = _time.Now.AddYears(11);

        var action = () => sut.EnsureCertificates(Target, "alexandria.home", new CertificateSettings());

        action.Should().Throw<InvalidOperationException>().WithMessage("*local CA*expired*trust the new public CA*");
        Snapshot().Should().BeEquivalentTo(original);
    }

    [Fact]
    public void EnsureCertificates_missingCaKey_neverCreatesReplacementTrustIdentity()
    {
        var sut = new CertificateService(_time);
        sut.EnsureCertificates(Target, "alexandria.home", new CertificateSettings());
        File.Delete(Path.Combine(Certificates, "local-ca.key"));
        var original = Snapshot();

        var action = () => sut.EnsureCertificates(Target, "alexandria.home", new CertificateSettings());

        action.Should().Throw<InvalidOperationException>().WithMessage("*local CA pair is incomplete*Restore*local-ca.key*");
        Snapshot().Should().BeEquivalentTo(original);
    }

    [Fact]
    public void EnsureCertificates_mismatchedCaKey_preservesBrokenPairForRecovery()
    {
        var sut = new CertificateService(_time);
        sut.EnsureCertificates(Target, "alexandria.home", new CertificateSettings());
        using var unrelated = RSA.Create(2048);
        File.WriteAllText(Path.Combine(Certificates, "local-ca.key"), unrelated.ExportPkcs8PrivateKeyPem());
        var original = Snapshot();

        var action = () => sut.EnsureCertificates(Target, "alexandria.home", new CertificateSettings());

        action.Should().Throw<InvalidOperationException>().WithMessage("*Existing local CA*private key does not match*");
        Snapshot().Should().BeEquivalentTo(original);
    }

    [Fact]
    public void EnsureCertificates_tamperedCaSignature_rejectsEvenThoughPrivateKeyMatches()
    {
        var sut = new CertificateService(_time);
        sut.EnsureCertificates(Target, "alexandria.home", new CertificateSettings());
        var path = Path.Combine(Certificates, "local-ca.crt");
        CorruptCertificateSignature(path);
        var original = Snapshot();

        var action = () => sut.EnsureCertificates(Target, "alexandria.home", new CertificateSettings());

        action.Should().Throw<InvalidOperationException>().WithMessage("*signature is invalid*");
        Snapshot().Should().BeEquivalentTo(original);
    }

    [Fact]
    public void EnsureCertificates_mismatchedExistingLeafWithoutPendingPair_preservesFiles()
    {
        var sut = new CertificateService(_time);
        sut.EnsureCertificates(Target, "alexandria.home", new CertificateSettings());
        using var unrelated = RSA.Create(2048);
        File.WriteAllText(Path.Combine(Certificates, "server.key"), unrelated.ExportPkcs8PrivateKeyPem());
        var original = Snapshot();

        var action = () => sut.EnsureCertificates(Target, "alexandria.home", new CertificateSettings());

        action.Should().Throw<InvalidOperationException>().WithMessage("*Existing server certificate*private key does not match*");
        Snapshot().Should().BeEquivalentTo(original);
    }

    [Theory]
    [InlineData(".local-ca.pending.pem")]
    [InlineData("local-ca.key")]
    [InlineData(".server.pending.pem")]
    [InlineData("server.key")]
    public void EnsureCertificates_interruptedPairInstallation_retryRetainsPendingIdentity(string stopAfter)
    {
        var sut = InterruptAfter(stopAfter);
        var action = () => sut.EnsureCertificates(Target, "192.168.0.106", new CertificateSettings());
        action.Should().Throw<IOException>().WithMessage("Simulated interruption");
        var pendingCa = Path.Combine(Certificates, ".local-ca.pending.pem");
        var caPath = File.Exists(pendingCa) ? pendingCa : Path.Combine(Certificates, "local-ca.crt");
        using var authorityBeforeRetry = X509Certificate2.CreateFromPemFile(caPath);
        var pendingLeaf = Path.Combine(Certificates, ".server.pending.pem");
        using var leafBeforeRetry = File.Exists(pendingLeaf) ? X509Certificate2.CreateFromPemFile(pendingLeaf) : null;

        var result = new CertificateService(_time).EnsureCertificates(Target, "192.168.0.106", new CertificateSettings());

        result.TrustSha256Fingerprint.Should().Be(authorityBeforeRetry.GetCertHashString(HashAlgorithmName.SHA256));

        if (leafBeforeRetry is not null)
        {
            result.CertificateSha256Fingerprint.Should().Be(leafBeforeRetry.GetCertHashString(HashAlgorithmName.SHA256));
        }

        Directory.GetFiles(Certificates).Should().HaveCount(4);
    }

    [Fact]
    public void EnsureCertificates_interruptedHostChange_reconcilesOldCertificateWithNewPendingKey()
    {
        var first = new CertificateService(_time).EnsureCertificates(Target, "192.168.0.106", new CertificateSettings());
        var action = () => InterruptAfter("server.key").EnsureCertificates(Target, "alexandria.home", new CertificateSettings());
        action.Should().Throw<IOException>();
        using var pending = X509Certificate2.CreateFromPemFile(Path.Combine(Certificates, ".server.pending.pem"));

        var result = new CertificateService(_time).EnsureCertificates(Target, "alexandria.home", new CertificateSettings());

        result.TrustSha256Fingerprint.Should().Be(first.TrustSha256Fingerprint);
        result.CertificateSha256Fingerprint.Should().Be(pending.GetCertHashString(HashAlgorithmName.SHA256));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnsureCertificates_importedSelfSignedPem_supportsRsaAndEcdsaWithoutChangingSource(bool ecdsa)
    {
        var settings = CreateImport("192.168.0.106", _time.Now.AddDays(-1), _time.Now.AddDays(30), ecdsa: ecdsa, authority: true);
        var sourceCertificate = File.ReadAllText(settings.ImportCertificatePath);
        var sourceKey = File.ReadAllText(settings.ImportPrivateKeyPath);
        var sut = new CertificateService(_time);

        sut.Validate(Target, "192.168.0.106", settings);
        Directory.Exists(Target).Should().BeFalse();
        var result = sut.EnsureCertificates(Target, "192.168.0.106", settings);
        using var installed = X509Certificate2.CreateFromPemFile(result.CertificatePath, result.PrivateKeyPath);

        installed.HasPrivateKey.Should().BeTrue();
        result.IsGenerated.Should().BeFalse();
        result.TrustCertificatePath.Should().Be(result.CertificatePath);
        result.RenewalInstructions.Should().Contain("self-signed replacement changes client trust");
        File.ReadAllText(settings.ImportCertificatePath).Should().Be(sourceCertificate);
        File.ReadAllText(settings.ImportPrivateKeyPath).Should().Be(sourceKey);
        Directory.GetFiles(Certificates).Should().HaveCount(3);
        sut.EnsureCertificates(Target, "192.168.0.106", settings).Reused.Should().BeTrue();
    }

    [Fact]
    public void EnsureCertificates_importedWildcardDnsSan_acceptsMatchingConcreteHostname()
    {
        var settings = CreateImport("*.example.test", _time.Now.AddDays(-1), _time.Now.AddDays(30));

        var result = new CertificateService(_time).EnsureCertificates(Target, "alexandria.example.test", settings);

        result.IsGenerated.Should().BeFalse();
        new CertificateService(_time).Validate(Target, "alexandria.example.test", settings);
        var wrongHost = () => new CertificateService(_time).Validate(Target, "nested.alexandria.example.test", settings);
        wrongHost.Should().Throw<InvalidOperationException>().WithMessage("*SAN does not cover*");
    }

    [Fact]
    public void EnsureCertificates_importSourcesRemoved_reusesRecordedTargetOwnedPair()
    {
        var settings = CreateImport("alexandria.home", _time.Now.AddDays(-1), _time.Now.AddDays(30));
        var sut = new CertificateService(_time);
        var first = sut.EnsureCertificates(Target, "alexandria.home", settings);
        File.Delete(settings.ImportCertificatePath);
        File.Delete(settings.ImportPrivateKeyPath);
        var original = Snapshot();

        sut.Validate(Target, "alexandria.home", settings);
        var details = sut.ReadDetails(Target, "alexandria.home", settings);
        var second = sut.EnsureCertificates(Target, "alexandria.home", settings);

        second.Reused.Should().BeTrue();
        second.CertificateSha256Fingerprint.Should().Be(first.CertificateSha256Fingerprint);
        details.CertificateSha256Fingerprint.Should().Be(first.CertificateSha256Fingerprint);
        Snapshot().Should().BeEquivalentTo(original);
    }

    [Fact]
    public void EnsureCertificates_newMissingImportSource_doesNotSilentlyReuseDifferentSelection()
    {
        var settings = CreateImport("alexandria.home", _time.Now.AddDays(-1), _time.Now.AddDays(30));
        var sut = new CertificateService(_time);
        sut.EnsureCertificates(Target, "alexandria.home", settings);
        var original = Snapshot();
        settings.ImportCertificatePath = Path.Combine(_directory, "mistyped-new-import.pem");
        var action = () => sut.EnsureCertificates(Target, "alexandria.home", settings);

        action.Should().Throw<InvalidOperationException>().WithMessage("*missing import sources do not identify*");
        Snapshot().Should().BeEquivalentTo(original);
    }

    [Fact]
    public void EnsureCertificates_importedFullChain_validatesOfflineAndCopiesOnlyPublicCertificates()
    {
        var generated = new CertificateService(_time).EnsureCertificates(Path.Combine(_directory, "issuer"), "alexandria.home", new CertificateSettings());
        var importDirectory = Path.Combine(_directory, "import");
        Directory.CreateDirectory(importDirectory);
        var settings = new CertificateSettings
        {
            Mode = CertificateSettings.ImportedMode,
            ImportCertificatePath = Path.Combine(importDirectory, "fullchain.pem"),
            ImportPrivateKeyPath = generated.PrivateKeyPath,
        };
        File.WriteAllText(settings.ImportCertificatePath, File.ReadAllText(generated.CertificatePath) + "\n" + File.ReadAllText(generated.TrustCertificatePath));

        var result = new CertificateService(_time).EnsureCertificates(Target, "alexandria.home", settings);
        var chain = new X509Certificate2Collection();

        try
        {
            chain.ImportFromPemFile(result.CertificatePath);
            chain.Should().HaveCount(2);
            File.ReadAllText(result.CertificatePath).Should().NotContain("PRIVATE KEY");
        }
        finally
        {
            foreach (var certificate in chain)
            {
                certificate.Dispose();
            }
        }
    }

    [Theory]
    [InlineData("1.3.6.1.5.5.7.3.3", false)]
    [InlineData("1.3.6.1.5.5.7.3.1", true)]
    public void EnsureCertificates_importedIntermediateEku_enforcesTlsServerPurposeAcrossChain(string intermediatePurpose, bool accepted)
    {
        const string host = "alexandria.home";
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest("CN=Test root", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 1, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var root = rootRequest.CreateSelfSigned(_time.Now.AddDays(-1), _time.Now.AddDays(30));
        using var intermediateKey = RSA.Create(2048);
        var intermediateRequest = new CertificateRequest("CN=Test intermediate", intermediateKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        intermediateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        intermediateRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        intermediateRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new(intermediatePurpose) }, false));
        using var intermediatePublic = intermediateRequest.Create(root, _time.Now.AddDays(-1), _time.Now.AddDays(20), RandomNumberGenerator.GetBytes(16));
        using var intermediate = intermediatePublic.CopyWithPrivateKey(intermediateKey);
        using var leafKey = RSA.Create(2048);
        var leafRequest = new CertificateRequest("CN=" + host, leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        leafRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        leafRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, false));
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(host);
        leafRequest.CertificateExtensions.Add(names.Build());
        using var leaf = leafRequest.Create(intermediate, _time.Now.AddDays(-1), _time.Now.AddDays(10), RandomNumberGenerator.GetBytes(16));
        Directory.CreateDirectory(_directory);
        var settings = new CertificateSettings
        {
            Mode = CertificateSettings.ImportedMode,
            ImportCertificatePath = Path.Combine(_directory, "chain.crt"),
            ImportPrivateKeyPath = Path.Combine(_directory, "chain.key"),
        };
        ProtectedFile.WriteAtomic(settings.ImportCertificatePath,
            leaf.ExportCertificatePem() + "\n" + intermediate.ExportCertificatePem() + "\n" + root.ExportCertificatePem());
        ProtectedFile.WriteAtomic(settings.ImportPrivateKeyPath, leafKey.ExportPkcs8PrivateKeyPem());
        var action = () => new CertificateService(_time).EnsureCertificates(Target, host, settings);

        if (accepted)
        {
            action.Should().NotThrow();
        }
        else
        {
            action.Should().Throw<InvalidOperationException>().WithMessage("*chain cannot be verified offline*");
            Directory.Exists(Target).Should().BeFalse();
        }
    }

    [Theory]
    [InlineData(-10, -1, "*expired*")]
    [InlineData(1, 10, "*not yet valid*")]
    public void EnsureCertificates_invalidImportDates_rejectsBeforeCreatingTarget(int notBeforeDays, int notAfterDays, string message)
    {
        var settings = CreateImport("alexandria.home", _time.Now.AddDays(notBeforeDays), _time.Now.AddDays(notAfterDays));
        var action = () => new CertificateService(_time).EnsureCertificates(Target, "alexandria.home", settings);

        action.Should().Throw<InvalidOperationException>().WithMessage(message);
        Directory.Exists(Target).Should().BeFalse();
    }

    [Theory]
    [InlineData("wrong.home", true)]
    [InlineData("alexandria.home", false)]
    public void EnsureCertificates_wrongOrMissingImportSan_rejectsBeforeCreatingTarget(string certificateHost, bool addSan)
    {
        var settings = CreateImport(certificateHost, _time.Now.AddDays(-1), _time.Now.AddDays(30), addSan: addSan);
        var action = () => new CertificateService(_time).EnsureCertificates(Target, "alexandria.home", settings);

        action.Should().Throw<InvalidOperationException>().WithMessage("*SAN does not cover*");
        Directory.Exists(Target).Should().BeFalse();
    }

    [Fact]
    public void EnsureCertificates_mismatchedImportedPrivateKey_rejectsBeforeCreatingTarget()
    {
        var settings = CreateImport("alexandria.home", _time.Now.AddDays(-1), _time.Now.AddDays(30));
        using var wrongKey = RSA.Create(2048);
        File.WriteAllText(settings.ImportPrivateKeyPath, wrongKey.ExportPkcs8PrivateKeyPem());
        var action = () => new CertificateService(_time).EnsureCertificates(Target, "alexandria.home", settings);

        action.Should().Throw<InvalidOperationException>().WithMessage("*private key does not match*");
        Directory.Exists(Target).Should().BeFalse();
    }

    [Fact]
    public void EnsureCertificates_tamperedSelfSignedImport_rejectsEvenThoughKeyMatches()
    {
        var settings = CreateImport("alexandria.home", _time.Now.AddDays(-1), _time.Now.AddDays(30));
        CorruptCertificateSignature(settings.ImportCertificatePath);
        var action = () => new CertificateService(_time).EnsureCertificates(Target, "alexandria.home", settings);

        action.Should().Throw<InvalidOperationException>().WithMessage("*signature is invalid*");
        Directory.Exists(Target).Should().BeFalse();
    }

    [Fact]
    public void EnsureCertificates_encryptedImportedKey_reportsSupportedFormatWithoutChangingTarget()
    {
        var settings = CreateImport("alexandria.home", _time.Now.AddDays(-1), _time.Now.AddDays(30));
        using var key = RSA.Create(2048);
        File.WriteAllText(settings.ImportPrivateKeyPath, key.ExportEncryptedPkcs8PrivateKeyPem("test-only-password",
            new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000)));
        var action = () => new CertificateService(_time).EnsureCertificates(Target, "alexandria.home", settings);

        action.Should().Throw<InvalidOperationException>().WithMessage("*encrypted private key*unsupported*");
        Directory.Exists(Target).Should().BeFalse();
    }

    [Fact]
    public void EnsureCertificates_publicInputContainsPrivateKey_rejectsInsteadOfPublishingSecret()
    {
        var settings = CreateImport("alexandria.home", _time.Now.AddDays(-1), _time.Now.AddDays(30));
        File.AppendAllText(settings.ImportCertificatePath, "\n" + File.ReadAllText(settings.ImportPrivateKeyPath));
        var action = () => new CertificateService(_time).EnsureCertificates(Target, "alexandria.home", settings);

        action.Should().Throw<InvalidOperationException>().WithMessage("*Do not place private keys in the public certificate file*");
        Directory.Exists(Target).Should().BeFalse();
    }

    [Fact]
    public void EnsureCertificates_unverifiableImportedIssuer_rejectsBeforeCreatingTarget()
    {
        var generated = new CertificateService(_time).EnsureCertificates(Path.Combine(_directory, "issuer"), "alexandria.home", new CertificateSettings());
        var settings = new CertificateSettings
        {
            Mode = CertificateSettings.ImportedMode,
            ImportCertificatePath = generated.CertificatePath,
            ImportPrivateKeyPath = generated.PrivateKeyPath,
        };
        var action = () => new CertificateService(_time).EnsureCertificates(Target, "alexandria.home", settings);

        action.Should().Throw<InvalidOperationException>().WithMessage("*chain cannot be verified offline*");
        Directory.Exists(Target).Should().BeFalse();
    }

    [Fact]
    public void EnsureCertificates_invalidImport_preservesExistingGeneratedInstallation()
    {
        var sut = new CertificateService(_time);
        sut.EnsureCertificates(Target, "alexandria.home", new CertificateSettings());
        var original = Snapshot();
        var settings = CreateImport("wrong.home", _time.Now.AddDays(-1), _time.Now.AddDays(30));
        var action = () => sut.EnsureCertificates(Target, "alexandria.home", settings);

        action.Should().Throw<InvalidOperationException>();
        Snapshot().Should().BeEquivalentTo(original);
    }

    [Theory]
    [InlineData("relative/target", "alexandria.home")]
    [InlineData("/tmp/unused-certificate-target", "https://alexandria.home")]
    [InlineData("/tmp/unused-certificate-target", "2001:db8::1")]
    public void Validate_unsupportedPathOrHost_rejectsBeforeWriting(string target, string host)
    {
        var action = () => new CertificateService(_time).Validate(target, host, new CertificateSettings());

        action.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnsureCertificates_symbolicLinkInTargetOrArtifact_rejectsWithoutChangingLinkedFiles(bool artifactLink)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Directory.CreateDirectory(_directory);
        var outside = Path.Combine(_directory, "outside");
        Directory.CreateDirectory(outside);
        var original = Path.Combine(outside, "original");
        File.WriteAllText(original, "preserve me");

        if (artifactLink)
        {
            Directory.CreateDirectory(Certificates);
            File.CreateSymbolicLink(Path.Combine(Certificates, "server.key"), original);
        }
        else
        {
            Directory.CreateSymbolicLink(Target, outside);
        }

        var action = () => new CertificateService(_time).EnsureCertificates(Target, "alexandria.home", new CertificateSettings());

        action.Should().Throw<InvalidOperationException>().WithMessage("*symbolic link*");
        File.ReadAllText(original).Should().Be("preserve me");
        Directory.GetFileSystemEntries(outside).Should().ContainSingle();
    }

    private CertificateService InterruptAfter(string fileName) => new(_time, (path, content, mode) =>
    {
        ProtectedFile.WriteAtomic(path, content, mode);

        if (Path.GetFileName(path) == fileName)
        {
            throw new IOException("Simulated interruption");
        }
    });

    private Dictionary<string, string> Snapshot() => Directory.GetFiles(Certificates)
        .ToDictionary(path => Path.GetFileName(path), File.ReadAllText);

    private static void CorruptCertificateSignature(string path)
    {
        using var certificate = X509Certificate2.CreateFromPem(File.ReadAllText(path));
        var bytes = certificate.RawData;
        bytes[^1] ^= 1;
        File.WriteAllText(path, PemEncoding.WriteString("CERTIFICATE", bytes));
    }

    private X509Chain CreateChain(X509Certificate2 authority)
    {
        var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(authority);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.DisableCertificateDownloads = true;
        chain.ChainPolicy.VerificationTime = _time.Now.UtcDateTime;

        return chain;
    }

    private CertificateSettings CreateImport(string host, DateTimeOffset notBefore, DateTimeOffset notAfter,
        bool ecdsa = false, bool authority = false, bool addSan = true)
    {
        Directory.CreateDirectory(_directory);
        var certificatePath = Path.Combine(_directory, $"import-{Guid.NewGuid():N}.crt");
        var keyPath = Path.ChangeExtension(certificatePath, ".key");
        using var rsaKey = RSA.Create(2048);
        using var ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        CertificateRequest request;
        string keyPem;

        if (ecdsa)
        {
            request = new CertificateRequest("CN=" + host, ecKey, HashAlgorithmName.SHA256);
            keyPem = ecKey.ExportPkcs8PrivateKeyPem();
        }
        else
        {
            request = new CertificateRequest("CN=" + host, rsaKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            keyPem = rsaKey.ExportPkcs8PrivateKeyPem();
        }

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(authority, false, 0, true));

        if (addSan)
        {
            var names = new SubjectAlternativeNameBuilder();

            if (System.Net.IPAddress.TryParse(host, out var address))
            {
                names.AddIpAddress(address);
                names.AddDnsName("localhost");
            }
            else
            {
                names.AddDnsName(host);
            }

            request.CertificateExtensions.Add(names.Build());
        }

        using var certificate = request.CreateSelfSigned(notBefore, notAfter);
        ProtectedFile.WriteAtomic(certificatePath, certificate.ExportCertificatePem());
        ProtectedFile.WriteAtomic(keyPath, keyPem);

        return new CertificateSettings
        {
            Mode = CertificateSettings.ImportedMode,
            ImportCertificatePath = certificatePath,
            ImportPrivateKeyPath = keyPath,
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
