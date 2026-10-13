using System.Formats.Asn1;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Builder.Models;
using Builder.Workflow;

namespace Builder.Services;

public sealed record CertificateResult(
    string CertificatePath,
    string PrivateKeyPath,
    string TrustCertificatePath,
    string CertificateSha256Fingerprint,
    string TrustSha256Fingerprint,
    DateTimeOffset ExpiresAt,
    bool IsGenerated,
    bool Reused,
    string RenewalInstructions);

public sealed class CertificateService
{
    private const UnixFileMode PrivateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode PublicMode = PrivateMode | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
    private const string ServerAuthentication = "1.3.6.1.5.5.7.3.1";
    private readonly TimeProvider _timeProvider;
    private readonly Action<string, string, UnixFileMode?> _write;

    public CertificateService(TimeProvider? timeProvider = null)
        : this(timeProvider, ProtectedFile.WriteAtomic)
    {
    }

    internal CertificateService(TimeProvider? timeProvider, Action<string, string, UnixFileMode?> write)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _write = write;
    }

    public void Validate(string installationPath, string host, CertificateSettings settings)
    {
        using var plan = Prepare(installationPath, host, settings, generate: false);
    }

    public CertificateResult ReadDetails(string installationPath, string host, CertificateSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ValidateHost(host);
        var paths = ValidatePaths(installationPath);
        using var plan = new Plan(paths, settings.Mode == CertificateSettings.GeneratedMode);

        if (!File.Exists(paths.Certificate) || !File.Exists(paths.Key))
        {
            throw new InvalidOperationException("Certificate files are not ready for these settings. Complete certificate generation or import before displaying their trust details.");
        }

        plan.Leaf = LoadPair(Read(paths.Certificate), Read(paths.Key), "Installed server certificate");
        ValidateServer(plan.Leaf.Certificate, host, allowAuthority: !plan.Generated);
        ValidateDates(plan.Leaf.Certificate, "Installed server certificate");

        if (plan.Generated)
        {
            plan.Authority = LoadPair(Read(paths.AuthorityCertificate), Read(paths.AuthorityKey), "Installed local CA");
            ValidateAuthority(plan.Authority.Certificate);
            VerifyGeneratedChain(plan.Leaf.Certificate, plan.Authority.Certificate);
        }
        else if (settings.Mode == CertificateSettings.ImportedMode)
        {
            VerifyImportedChain(plan.Leaf);
        }
        else
        {
            throw new InvalidOperationException("Choose certificate mode 'generated' or 'imported'.");
        }

        return Describe(plan, reused: true);
    }

    public CertificateResult EnsureCertificates(string installationPath, string host, CertificateSettings settings)
    {
        using var plan = Prepare(installationPath, host, settings, generate: true);
        var paths = plan.Paths;
        var leaf = plan.Leaf!;

        // Durable protected pairs retain the trust identity if replacement stops between files.
        Directory.CreateDirectory(paths.Directory);

        if (plan.WriteAuthority)
        {
            WritePair(paths.AuthorityPending, paths.AuthorityCertificate, paths.AuthorityKey, plan.Authority!);
        }

        if (plan.Authority is not null)
        {
            using var installedAuthority = LoadPair(Read(paths.AuthorityCertificate), Read(paths.AuthorityKey), "Installed local CA");
            ValidateAuthority(installedAuthority.Certificate);

            if (!SameCertificate(installedAuthority.Certificate, plan.Authority.Certificate))
            {
                throw new InvalidOperationException("The installed local CA differs from the validated trust identity. Restore its matching pair before retrying.");
            }
        }

        if (plan.WriteLeaf)
        {
            WritePair(paths.LeafPending, paths.Certificate, paths.Key, leaf);
        }

        using (var verified = LoadPair(Read(paths.Certificate), Read(paths.Key), "Installed server certificate"))
        {
            ValidateServer(verified.Certificate, host, allowAuthority: !plan.Generated);
            ValidateDates(verified.Certificate, "Installed server certificate");

            if (plan.Generated)
            {
                VerifyGeneratedChain(verified.Certificate, plan.Authority!.Certificate);
            }
            else
            {
                VerifyImportedChain(verified);
            }
        }

        SetMode(paths.Key, PrivateMode);
        SetMode(paths.Certificate, PublicMode);

        if (plan.Authority is not null)
        {
            SetMode(paths.AuthorityKey, PrivateMode);
            SetMode(paths.AuthorityCertificate, PublicMode);
        }

        if (plan.ImportReceipt is not null && !FilesEqual(paths.ImportSource, plan.ImportReceipt))
        {
            Write(paths.ImportSource, plan.ImportReceipt, PrivateMode);
        }

        DeletePending(paths.LeafPending);

        if (plan.Generated)
        {
            DeletePending(paths.AuthorityPending);
        }

        return Describe(plan, !plan.WriteLeaf && !plan.WriteAuthority);
    }

    private static CertificateResult Describe(Plan plan, bool reused)
    {
        var paths = plan.Paths;
        var leaf = plan.Leaf!;
        var trust = plan.Authority?.Certificate ?? leaf.Certificate;
        var trustPath = plan.Generated ? paths.AuthorityCertificate : paths.Certificate;
        var instructions = plan.Generated
            ? "Rerun Builder during the last 30 days before server certificate expiry to renew it using the same local CA. " +
              "Back up local-ca.crt and local-ca.key securely together. If the CA expires or its key is lost, " +
              "explicitly replace the CA and server pair after backing them up, then trust the new public CA on each client."
            : "Before expiry, renew the imported certificate with its issuer, select the renewed PEM certificate and matching " +
              "unencrypted PEM private key in Builder, and rerun. A self-signed replacement changes client trust; " +
              "Android clients should use an installation-owned local CA or an issuer they already trust.";

        return new CertificateResult(paths.Certificate, paths.Key, trustPath,
            Fingerprint(leaf.Certificate), Fingerprint(trust),
            new DateTimeOffset(leaf.Certificate.NotAfter.ToUniversalTime()), plan.Generated,
            reused, instructions);
    }

    private Plan Prepare(string installationPath, string host, CertificateSettings settings, bool generate)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ValidateHost(host);
        var paths = ValidatePaths(installationPath);
        var plan = new Plan(paths, settings.Mode == CertificateSettings.GeneratedMode);

        try
        {
            if (settings.Mode == CertificateSettings.ImportedMode)
            {
                ValidateImportPath(settings.ImportCertificatePath, "certificate");
                ValidateImportPath(settings.ImportPrivateKeyPath, "private key");
                var sourceIdentity = ImportIdentity(settings);

                if (File.Exists(settings.ImportCertificatePath) && File.Exists(settings.ImportPrivateKeyPath))
                {
                    plan.Leaf = LoadPair(Read(settings.ImportCertificatePath), Read(settings.ImportPrivateKeyPath), "Imported certificate");
                }
                else
                {
                    if (!File.Exists(paths.ImportSource) || !File.Exists(paths.Certificate) || !File.Exists(paths.Key))
                    {
                        throw new InvalidOperationException("The selected PEM import source is missing and no matching target-owned import is recorded. " +
                            "Restore the original source pair or select existing absolute PEM certificate/key paths. Existing files were preserved.");
                    }

                    plan.Leaf = LoadPair(Read(paths.Certificate), Read(paths.Key), "Installed imported certificate");

                    if (Read(paths.ImportSource) != sourceIdentity + "\n" + Fingerprint(plan.Leaf.Certificate) + "\n")
                    {
                        throw new InvalidOperationException("The missing import sources do not identify the installed imported pair. " +
                            "Restore the previously selected source pair or select an existing replacement pair. Existing files were preserved.");
                    }
                }

                ValidateServer(plan.Leaf.Certificate, host, allowAuthority: true);
                ValidateDates(plan.Leaf.Certificate, "Imported certificate");
                VerifyImportedChain(plan.Leaf);
                ValidatePendingImport(paths, plan.Leaf);
                plan.WriteLeaf = !FilesEqual(paths.Certificate, plan.Leaf.CertificatePem) || !KeyMatches(paths.Key, plan.Leaf.Certificate);
                plan.ImportReceipt = sourceIdentity + "\n" + Fingerprint(plan.Leaf.Certificate) + "\n";

                return plan;
            }

            if (settings.Mode != CertificateSettings.GeneratedMode)
            {
                throw new InvalidOperationException("Choose certificate mode 'generated' or 'imported'. No certificate files were changed.");
            }

            plan.Authority = ReadAuthority(paths, out var writeAuthority);
            plan.WriteAuthority = writeAuthority;

            if (plan.Authority is null)
            {
                if (File.Exists(paths.Certificate) || File.Exists(paths.Key) || File.Exists(paths.LeafPending))
                {
                    throw new InvalidOperationException("Existing server material has no installation-owned local CA. " +
                        "Import the existing certificate and key explicitly, or restore the matching local-ca.crt and local-ca.key. Existing files were preserved.");
                }

                if (!generate)
                {
                    return plan;
                }

                plan.Authority = CreateAuthority();
                plan.WriteAuthority = true;
            }

            plan.Leaf = ReadGeneratedLeaf(paths, plan.Authority.Certificate, host, out var writeLeaf);
            plan.WriteLeaf = writeLeaf;

            if (plan.Leaf is null && generate)
            {
                plan.Leaf = CreateLeaf(host, plan.Authority.Certificate);
                plan.WriteLeaf = true;
            }

            return plan;
        }
        catch
        {
            plan.Dispose();

            throw;
        }
    }

    private Material? ReadAuthority(Paths paths, out bool write)
    {
        write = false;
        Material? pending = null;

        try
        {
            if (File.Exists(paths.AuthorityPending))
            {
                var content = Read(paths.AuthorityPending);
                pending = LoadPair(content, content, "Pending local CA", allowPrivateInCertificate: true);
                ValidateAuthority(pending.Certificate);
            }

            var certificateExists = File.Exists(paths.AuthorityCertificate);
            var keyExists = File.Exists(paths.AuthorityKey);

            if (certificateExists && keyExists)
            {
                var existing = LoadPair(Read(paths.AuthorityCertificate), Read(paths.AuthorityKey), "Existing local CA");

                try
                {
                    ValidateAuthority(existing.Certificate);

                    if (pending is not null && !SameCertificate(existing.Certificate, pending.Certificate))
                    {
                        throw new InvalidOperationException("Pending and installed local CA identities differ. Restore the matching CA pair before retrying. Existing files were preserved.");
                    }

                    return existing;
                }
                catch
                {
                    existing.Dispose();

                    throw;
                }
            }

            if (pending is not null)
            {
                if ((certificateExists && !CertificateMatches(paths.AuthorityCertificate, pending.Certificate)) ||
                    (keyExists && !KeyMatches(paths.AuthorityKey, pending.Certificate)))
                {
                    throw new InvalidOperationException("The partial local CA does not match its pending recovery pair. Restore the original CA files before retrying. Existing files were preserved.");
                }

                write = true;
                var recovered = pending;
                pending = null;

                return recovered;
            }

            if (certificateExists || keyExists)
            {
                throw new InvalidOperationException("The local CA pair is incomplete. Restore the missing local-ca.crt or local-ca.key from backup. " +
                    "Builder will not replace its trust identity silently; explicit CA replacement requires trusting the new public CA on every client.");
            }

            return null;
        }
        finally
        {
            pending?.Dispose();
        }
    }

    private Material? ReadGeneratedLeaf(Paths paths, X509Certificate2 authority, string host, out bool write)
    {
        write = false;
        Material? pending = null;

        try
        {
            if (File.Exists(paths.LeafPending))
            {
                var content = Read(paths.LeafPending);
                pending = LoadPair(content, content, "Pending server certificate", allowPrivateInCertificate: true);
                ValidateServerPurpose(pending.Certificate, allowAuthority: false);
                VerifyGeneratedChain(pending.Certificate, authority, ignoreTime: true);
            }

            var certificateExists = File.Exists(paths.Certificate);
            var keyExists = File.Exists(paths.Key);
            Material? existing = null;

            if (certificateExists && keyExists)
            {
                try
                {
                    existing = LoadPair(Read(paths.Certificate), Read(paths.Key), "Existing server certificate");
                }
                catch (InvalidOperationException) when (pending is not null &&
                    (CertificateMatches(paths.Certificate, pending.Certificate) || KeyMatches(paths.Key, pending.Certificate)))
                {
                    // A pending pair permits recovery after just one final file was replaced.
                }
            }

            if (existing is not null)
            {
                try
                {
                    ValidateServerPurpose(existing.Certificate, allowAuthority: false);
                    VerifyGeneratedChain(existing.Certificate, authority, ignoreTime: true);
                    RejectFutureCertificate(existing.Certificate, "Existing server certificate");

                    if (pending is null && IsReusableLeaf(existing.Certificate, authority) && CoversHost(existing.Certificate, host))
                    {
                        return existing;
                    }
                }
                catch
                {
                    existing.Dispose();

                    throw;
                }

                existing.Dispose();
            }
            else if (certificateExists || keyExists)
            {
                if (pending is null ||
                    (certificateExists && !CertificateMatches(paths.Certificate, pending.Certificate) &&
                     (!keyExists || !KeyMatches(paths.Key, pending.Certificate))) ||
                    (keyExists && !KeyMatches(paths.Key, pending.Certificate) &&
                     (!certificateExists || !CertificateMatches(paths.Certificate, pending.Certificate))))
                {
                    throw new InvalidOperationException("The server certificate pair is incomplete or does not match its recovery pair. " +
                        "Restore server.crt and server.key together before retrying. The local CA and existing files were preserved.");
                }
            }

            write = true;

            if (pending is not null && IsReusableLeaf(pending.Certificate, authority) && CoversHost(pending.Certificate, host))
            {
                var recovered = pending;
                pending = null;

                return recovered;
            }

            return null;
        }
        finally
        {
            pending?.Dispose();
        }
    }

    private void ValidatePendingImport(Paths paths, Material imported)
    {
        if (!File.Exists(paths.LeafPending))
        {
            return;
        }

        var content = Read(paths.LeafPending);
        using var pending = LoadPair(content, content, "Pending server certificate", allowPrivateInCertificate: true);

        if (!SameCertificate(imported.Certificate, pending.Certificate))
        {
            throw new InvalidOperationException("The pending certificate differs from the selected import. Retry with the original imported pair " +
                "or back up and resolve the pending recovery file before selecting a replacement. Existing files were preserved.");
        }
    }

    private Material CreateAuthority()
    {
        using var key = RSA.Create(3072);
        var request = new CertificateRequest("CN=Alexandria installation local CA", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        var now = _timeProvider.GetUtcNow();
        using var certificate = request.CreateSelfSigned(now.AddMinutes(-5), now.AddYears(10));

        return LoadPair(certificate.ExportCertificatePem(), key.ExportPkcs8PrivateKeyPem(), "Generated local CA");
    }

    private Material CreateLeaf(string host, X509Certificate2 authority)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Alexandria", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new(ServerAuthentication) }, false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        var names = new SubjectAlternativeNameBuilder();

        if (IPAddress.TryParse(host, out var address))
        {
            names.AddIpAddress(address);
        }
        else
        {
            names.AddDnsName(host);
        }

        request.CertificateExtensions.Add(names.Build());
        var now = _timeProvider.GetUtcNow();
        var expiry = now.AddDays(365);
        var authorityExpiry = new DateTimeOffset(authority.NotAfter.ToUniversalTime());

        if (expiry > authorityExpiry)
        {
            expiry = authorityExpiry;
        }

        using var certificate = request.Create(authority, now.AddMinutes(-5), expiry, RandomNumberGenerator.GetBytes(16));
        var material = LoadPair(certificate.ExportCertificatePem(), key.ExportPkcs8PrivateKeyPem(), "Generated server certificate");

        try
        {
            ValidateServer(material.Certificate, host, allowAuthority: false);
            VerifyGeneratedChain(material.Certificate, authority);

            return material;
        }
        catch
        {
            material.Dispose();

            throw;
        }
    }

    private void ValidateAuthority(X509Certificate2 certificate)
    {
        ValidateDates(certificate, "Existing local CA");
        var constraints = certificate.Extensions.OfType<X509BasicConstraintsExtension>().SingleOrDefault();
        var usage = certificate.Extensions.OfType<X509KeyUsageExtension>().SingleOrDefault();

        if (constraints?.CertificateAuthority != true || usage is null ||
            (usage.KeyUsages & X509KeyUsageFlags.KeyCertSign) == 0 ||
            !certificate.SubjectName.RawData.AsSpan().SequenceEqual(certificate.IssuerName.RawData))
        {
            throw new InvalidOperationException("The local CA is not a valid self-issued signing authority. Restore its original pair from backup. Existing files were preserved.");
        }

        VerifySelfSignature(certificate);
        VerifyGeneratedChain(certificate, certificate);
    }

    private void ValidateDates(X509Certificate2 certificate, string description)
    {
        RejectFutureCertificate(certificate, description);

        if (!IsCurrent(certificate))
        {
            throw new InvalidOperationException(description + " has expired. Renew an imported pair with its issuer. " +
                "For an expired local CA, back up the CA and server pairs before explicitly replacing them and trust the new public CA on every client. Existing files were preserved.");
        }
    }

    private void RejectFutureCertificate(X509Certificate2 certificate, string description)
    {
        if (_timeProvider.GetUtcNow().UtcDateTime < certificate.NotBefore.ToUniversalTime())
        {
            throw new InvalidOperationException(description + " is not yet valid. Check the system clock or select a currently valid certificate. Existing files were preserved.");
        }
    }

    private bool IsCurrent(X509Certificate2 certificate) =>
        certificate.NotBefore.ToUniversalTime() <= _timeProvider.GetUtcNow().UtcDateTime &&
        _timeProvider.GetUtcNow().UtcDateTime < certificate.NotAfter.ToUniversalTime();

    private bool IsReusableLeaf(X509Certificate2 certificate, X509Certificate2 authority) => IsCurrent(certificate) &&
        (certificate.NotAfter.ToUniversalTime() > _timeProvider.GetUtcNow().AddDays(30).UtcDateTime ||
         certificate.NotAfter.ToUniversalTime() == authority.NotAfter.ToUniversalTime());

    private static void ValidateServer(X509Certificate2 certificate, string host, bool allowAuthority)
    {
        ValidateServerPurpose(certificate, allowAuthority);

        if (!CoversHost(certificate, host))
        {
            throw new InvalidOperationException("The server certificate SAN does not cover the selected LAN host or IP. " +
                "Select a certificate with that DNS/IP subject alternative name; a common name alone is insufficient. Existing files were preserved.");
        }
    }

    private static void ValidateServerPurpose(X509Certificate2 certificate, bool allowAuthority)
    {
        var constraints = certificate.Extensions.OfType<X509BasicConstraintsExtension>().SingleOrDefault();
        var usage = certificate.Extensions.OfType<X509KeyUsageExtension>().SingleOrDefault();
        var enhancedUsage = certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().SingleOrDefault();

        if ((!allowAuthority && constraints?.CertificateAuthority == true) ||
            (usage is not null && (usage.KeyUsages & (X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment)) == 0) ||
            (enhancedUsage is not null && !enhancedUsage.EnhancedKeyUsages.Cast<Oid>().Any(oid => oid.Value == ServerAuthentication || oid.Value == "2.5.29.37.0")))
        {
            throw new InvalidOperationException("The selected certificate is not permitted for TLS server authentication. Select a server certificate and matching private key. Existing files were preserved.");
        }
    }

    private static bool CoversHost(X509Certificate2 certificate, string host) =>
        certificate.MatchesHostname(host, allowWildcards: true, allowCommonName: false);

    private void VerifyGeneratedChain(X509Certificate2 leaf, X509Certificate2 authority, bool ignoreTime = false)
    {
        using var chain = CreateChain(ignoreTime);
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(authority);

        if (!SameCertificate(leaf, authority))
        {
            chain.ChainPolicy.ApplicationPolicy.Add(new Oid(ServerAuthentication));
        }

        if (!chain.Build(leaf) || !SameCertificate(chain.ChainElements[^1].Certificate, authority))
        {
            throw new InvalidOperationException("The certificate does not validate against the installation-owned local CA. " +
                "Restore the matching CA and server pairs from backup. Existing files were preserved.");
        }
    }

    private void VerifyImportedChain(Material material)
    {
        var certificates = new X509Certificate2Collection();

        try
        {
            certificates.ImportFromPem(material.CertificatePem);
            using var chain = CreateChain(ignoreTime: false);
            chain.ChainPolicy.ApplicationPolicy.Add(new Oid(ServerAuthentication));

            foreach (var certificate in certificates)
            {
                ValidateDates(certificate, "Imported certificate chain");
                chain.ChainPolicy.ExtraStore.Add(certificate);
            }

            var last = certificates[^1];

            if (last.SubjectName.RawData.AsSpan().SequenceEqual(last.IssuerName.RawData))
            {
                VerifySelfSignature(last);
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.Add(last);
            }

            if (!chain.Build(material.Certificate))
            {
                throw new InvalidOperationException("The imported certificate chain cannot be verified offline. " +
                    "Provide PEM certificates in server-first order, followed by the issuing intermediates and local root CA if used. " +
                    "A self-signed certificate with a matching SAN and key is also supported. Existing files were preserved.");
            }
        }
        finally
        {
            foreach (var certificate in certificates)
            {
                certificate.Dispose();
            }
        }
    }

    private static void VerifySelfSignature(X509Certificate2 certificate)
    {
        try
        {
            var reader = new AsnReader(certificate.RawData, AsnEncodingRules.DER);
            var sequence = reader.ReadSequence();
            var signedContent = sequence.ReadEncodedValue();
            sequence.ReadEncodedValue();
            var signature = sequence.ReadBitString(out var unusedBits);
            sequence.ThrowIfNotEmpty();
            reader.ThrowIfNotEmpty();
            var algorithm = certificate.SignatureAlgorithm.Value;
            var hash = algorithm switch
            {
                "1.2.840.113549.1.1.5" or "1.2.840.10045.4.1" => HashAlgorithmName.SHA1,
                "1.2.840.113549.1.1.11" or "1.2.840.10045.4.3.2" => HashAlgorithmName.SHA256,
                "1.2.840.113549.1.1.12" or "1.2.840.10045.4.3.3" => HashAlgorithmName.SHA384,
                "1.2.840.113549.1.1.13" or "1.2.840.10045.4.3.4" => HashAlgorithmName.SHA512,
                _ => throw new InvalidOperationException("The self-signed certificate uses an unsupported signature algorithm. Use an RSA PKCS#1 or ECDSA signed PEM certificate. Existing files were preserved."),
            };
            var valid = false;

            using var rsa = certificate.GetRSAPublicKey();

            if (rsa is not null && algorithm!.StartsWith("1.2.840.113549.1.1.", StringComparison.Ordinal))
            {
                valid = rsa.VerifyData(signedContent.Span, signature, hash, RSASignaturePadding.Pkcs1);
            }
            else
            {
                using var ecdsa = certificate.GetECDsaPublicKey();

                if (ecdsa is not null && algorithm!.StartsWith("1.2.840.10045.", StringComparison.Ordinal))
                {
                    valid = ecdsa.VerifyData(signedContent.Span, signature, hash, DSASignatureFormat.Rfc3279DerSequence);
                }
            }

            if (!valid || unusedBits != 0)
            {
                throw new InvalidOperationException("The self-signed certificate signature is invalid. Restore the original certificate and matching key. Existing files were preserved.");
            }
        }
        catch (Exception exception) when (exception is AsnContentException or CryptographicException)
        {
            throw new InvalidOperationException("The self-signed certificate signature could not be verified. Restore the original certificate and matching key. Existing files were preserved.");
        }
    }

    private X509Chain CreateChain(bool ignoreTime)
    {
        var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.DisableCertificateDownloads = true;
        chain.ChainPolicy.VerificationTime = _timeProvider.GetUtcNow().UtcDateTime;
        chain.ChainPolicy.VerificationFlags = ignoreTime ? X509VerificationFlags.IgnoreNotTimeValid : X509VerificationFlags.NoFlag;

        return chain;
    }

    private static Material LoadPair(string certificatePem, string keyPem, string description, bool allowPrivateInCertificate = false)
    {
        if (keyPem.Contains("ENCRYPTED", StringComparison.Ordinal) || keyPem.Contains("Proc-Type:", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(description + " uses an encrypted private key. Export an unencrypted PEM key into a protected file for Nginx; encrypted keys and PKCS#12/DER imports are unsupported. Existing files were preserved.");
        }

        if ((!allowPrivateInCertificate && certificatePem.Contains("PRIVATE KEY", StringComparison.Ordinal)) ||
            !certificatePem.Contains("-----BEGIN CERTIFICATE-----", StringComparison.Ordinal) ||
            !keyPem.Contains("PRIVATE KEY-----", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(description + " requires a PEM certificate and a separate unencrypted RSA or ECDSA PEM private key. " +
                "Do not place private keys in the public certificate file. Existing files were preserved.");
        }

        try
        {
            var certificate = X509Certificate2.CreateFromPem(certificatePem, keyPem);
            string privateKey;
            using var rsa = certificate.GetRSAPrivateKey();

            if (rsa is not null)
            {
                privateKey = rsa.ExportPkcs8PrivateKeyPem();
            }
            else
            {
                using var ecdsa = certificate.GetECDsaPrivateKey();

                if (ecdsa is null)
                {
                    certificate.Dispose();

                    throw new InvalidOperationException("Only RSA and ECDSA PEM private keys are supported. Existing files were preserved.");
                }

                privateKey = ecdsa.ExportPkcs8PrivateKeyPem();
            }

            var publicPem = new X509Certificate2Collection();

            try
            {
                publicPem.ImportFromPem(certificatePem);
                var publicContent = string.Join("\n", publicPem.Cast<X509Certificate2>().Select(item => item.ExportCertificatePem())) + "\n";

                return new Material(certificate, publicContent, privateKey + "\n");
            }
            finally
            {
                foreach (var item in publicPem)
                {
                    item.Dispose();
                }
            }
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException(description + " is invalid or its private key does not match. " +
                "Restore or select the matching unencrypted PEM pair. Existing files were preserved.");
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException(description + " is not a supported RSA/ECDSA PEM pair. Existing files were preserved.");
        }
    }

    private void WritePair(string pendingPath, string certificatePath, string keyPath, Material material)
    {
        Write(pendingPath, material.CertificatePem + material.PrivateKeyPem, PrivateMode);
        Write(keyPath, material.PrivateKeyPem, PrivateMode);
        Write(certificatePath, material.CertificatePem, PublicMode);
    }

    private void Write(string path, string content, UnixFileMode mode)
    {
        ValidateFilePath(path);
        _write(path, content, mode);
    }

    private static void SetMode(string path, UnixFileMode mode)
    {
        ValidateFilePath(path);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, mode);
        }
    }

    private static void DeletePending(string path)
    {
        ValidateFilePath(path);
        File.Delete(path);
    }

    private static bool FilesEqual(string path, string content) => File.Exists(path) && Read(path) == content;

    private static bool CertificateMatches(string path, X509Certificate2 certificate)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var other = X509Certificate2.CreateFromPem(Read(path));

            return SameCertificate(other, certificate);
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException)
        {
            return false;
        }
    }

    private static bool KeyMatches(string path, X509Certificate2 certificate)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var other = X509Certificate2.CreateFromPem(certificate.ExportCertificatePem(), Read(path));

            return other.HasPrivateKey;
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException)
        {
            return false;
        }
    }

    private static bool SameCertificate(X509Certificate2 first, X509Certificate2 second) =>
        first.RawData.AsSpan().SequenceEqual(second.RawData);

    private static string Fingerprint(X509Certificate2 certificate) => certificate.GetCertHashString(HashAlgorithmName.SHA256);

    private static string Read(string path)
    {
        try
        {
            var file = new FileInfo(path);

            if (!file.Exists || file.Length > 1024 * 1024)
            {
                throw new InvalidOperationException("A certificate input is missing or exceeds the supported 1 MiB PEM size. Check the selected certificate/key paths. Existing files were preserved.");
            }

            return File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException("A certificate input could not be read. Check the selected paths and file permissions. Existing files were preserved.");
        }
    }

    private static void ValidateImportPath(string path, string description)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Any(char.IsControl) || !Path.IsPathFullyQualified(path))
        {
            throw new InvalidOperationException("Select an existing absolute PEM " + description + " path before importing. Existing files were preserved.");
        }
    }

    private static string ImportIdentity(CertificateSettings settings)
    {
        var certificate = Path.GetFullPath(settings.ImportCertificatePath);
        var key = Path.GetFullPath(settings.ImportPrivateKeyPath);

        if (OperatingSystem.IsWindows())
        {
            certificate = certificate.ToUpperInvariant();
            key = key.ToUpperInvariant();
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(certificate + "\0" + key)));
    }

    private static void ValidateHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Any(char.IsControl) || host != host.Trim())
        {
            throw new InvalidOperationException("Select a valid LAN IPv4 address or DNS hostname before preparing certificates.");
        }

        if (IPAddress.TryParse(host, out var address))
        {
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                return;
            }

            throw new InvalidOperationException("IPv6 certificates are not supported by this installer yet. Select a LAN IPv4 address or DNS hostname.");
        }

        if (Uri.CheckHostName(host) != UriHostNameType.Dns || host.Contains('*') || host.Contains(':') || host.Contains('/'))
        {
            throw new InvalidOperationException("Select a valid DNS hostname without a URL scheme, port, path, or wildcard before preparing certificates.");
        }
    }

    private static Paths ValidatePaths(string installationPath)
    {
        if (string.IsNullOrWhiteSpace(installationPath) || installationPath.Any(char.IsControl) || !Path.IsPathFullyQualified(installationPath))
        {
            throw new InvalidOperationException("Certificate artifacts require the resolved absolute installation folder.");
        }

        var paths = new Paths(Path.Combine(Path.GetFullPath(installationPath), "nginx", "certs"));

        foreach (var path in new[] { paths.Certificate, paths.Key, paths.AuthorityCertificate, paths.AuthorityKey, paths.AuthorityPending, paths.LeafPending, paths.ImportSource })
        {
            ValidateFilePath(path);
        }

        return paths;
    }

    private static void ValidateFilePath(string path)
    {
        var error = PathResolver.ValidateTarget(Path.GetDirectoryName(path)!, probeWritable: false);

        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }

        try
        {
            var attributes = File.GetAttributes(path);

            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            {
                throw new InvalidOperationException("A certificate artifact is an existing directory or symbolic link. Choose real target files before retrying. Existing files were preserved.");
            }
        }
        catch (FileNotFoundException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    private sealed record Paths(string Directory)
    {
        public string Certificate => Path.Combine(Directory, "server.crt");
        public string Key => Path.Combine(Directory, "server.key");
        public string AuthorityCertificate => Path.Combine(Directory, "local-ca.crt");
        public string AuthorityKey => Path.Combine(Directory, "local-ca.key");
        public string AuthorityPending => Path.Combine(Directory, ".local-ca.pending.pem");
        public string LeafPending => Path.Combine(Directory, ".server.pending.pem");
        public string ImportSource => Path.Combine(Directory, "import-source.sha256");
    }

    private sealed record Material(X509Certificate2 Certificate, string CertificatePem, string PrivateKeyPem) : IDisposable
    {
        public void Dispose() => Certificate.Dispose();
    }

    private sealed class Plan(Paths paths, bool generated) : IDisposable
    {
        public Paths Paths { get; } = paths;
        public bool Generated { get; } = generated;
        public Material? Authority { get; set; }
        public Material? Leaf { get; set; }
        public bool WriteAuthority { get; set; }
        public bool WriteLeaf { get; set; }
        public string? ImportReceipt { get; set; }

        public void Dispose()
        {
            Authority?.Dispose();
            Leaf?.Dispose();
        }
    }
}
