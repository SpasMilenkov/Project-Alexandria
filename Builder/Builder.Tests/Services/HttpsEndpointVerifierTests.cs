using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AwesomeAssertions;
using Builder.Services;

namespace Builder.Tests.Services;

public class HttpsEndpointVerifierTests
{
    [Theory]
    [InlineData(SslPolicyErrors.None, true)]
    [InlineData(SslPolicyErrors.RemoteCertificateChainErrors, true)]
    [InlineData(SslPolicyErrors.RemoteCertificateNameMismatch, false)]
    [InlineData(SslPolicyErrors.RemoteCertificateNotAvailable, false)]
    public void CertificateMatches_installationLeaf_rejectsWrongHostnameAndAllowsOnlyScopedTrust(SslPolicyErrors errors, bool accepted)
    {
        using var certificate = CreateCertificate();

        HttpsEndpointVerifier.CertificateMatches(certificate, errors, certificate).Should().Be(accepted);
    }

    [Fact]
    public void CertificateMatches_differentCertificateWithSameHostname_rejects()
    {
        using var expected = CreateCertificate();
        using var impostor = CreateCertificate();

        HttpsEndpointVerifier.CertificateMatches(impostor, SslPolicyErrors.None, expected).Should().BeFalse();
    }

    [Fact]
    public void CertificateMatches_expiredPinnedCertificate_rejects()
    {
        using var expected = CreateCertificate(expired: true);

        HttpsEndpointVerifier.CertificateMatches(expected, SslPolicyErrors.None, expected).Should().BeFalse();
    }

    [Fact]
    public void CertificateMatches_notYetValidPinnedCertificate_rejects()
    {
        using var expected = CreateCertificate(notYetValid: true);

        HttpsEndpointVerifier.CertificateMatches(expected, SslPolicyErrors.None, expected).Should().BeFalse();
    }

    private static X509Certificate2 CreateCertificate(bool expired = false, bool notYetValid = false)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=alexandria.example", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var alternativeNames = new SubjectAlternativeNameBuilder();
        alternativeNames.AddDnsName("alexandria.example");
        request.CertificateExtensions.Add(alternativeNames.Build());
        var now = DateTimeOffset.UtcNow;

        var notBefore = notYetValid ? now.AddHours(1) : now.AddDays(-2);

        return request.CreateSelfSigned(notBefore, expired ? now.AddDays(-1) : now.AddDays(1));
    }
}
