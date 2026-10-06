using System.Text.RegularExpressions;
using AwesomeAssertions;
using Builder.Services;

namespace Builder.Tests.Services;

public class CredentialServiceTests
{
    private static CredentialService CreateSut() => new();

    [Fact]
    public void GenerateHexSecret_defaultLength_returns64LowercaseHexCharacters()
    {
        var sut = CreateSut();

        var secret = sut.GenerateHexSecret();

        secret.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void GenerateHexSecret_withCustomByteCount_returnsDoubleThatCountInHex()
    {
        var sut = CreateSut();

        var secret = sut.GenerateHexSecret(16);

        secret.Should().HaveLength(32);
    }

    [Fact]
    public void GenerateHexSecret_calledTwice_producesDistinctValues()
    {
        var sut = CreateSut();

        var first = sut.GenerateHexSecret();
        var second = sut.GenerateHexSecret();

        first.Should().NotBe(second);
    }

    [Fact]
    public void GeneratePassword_defaultLength_returns24CharsFromAllowedCharset()
    {
        var sut = CreateSut();

        var password = sut.GeneratePassword();

        password.Should().MatchRegex("^[A-Za-z0-9!@#$%^&*]{24}$");
    }

    [Fact]
    public void GenerateAllCredentials_always_containsCoreCredentialKeys()
    {
        var sut = CreateSut();
        var features = new Builder.Models.FeatureSelection();

        var credentials = sut.GenerateAllCredentials(features);

        var expectedKeys = new[]
        {
            "DB_PASSWORD",
            "GARAGE_RPC_SECRET",
            "GARAGE_ADMIN_TOKEN",
            "GARAGE_METRICS_TOKEN",
            "RABBITMQ_USER",
            "RABBITMQ_PASSWORD",
            "RABBITMQ_VHOST",
            "API_RABBITMQ_PASSWORD",
            "DOCUMENT_WORKER_RABBITMQ_PASSWORD",
            "MEDIA_WORKER_RABBITMQ_PASSWORD",
            "TRANSPILATION_WORKER_RABBITMQ_PASSWORD",
            "MEDIA_METADATA_WORKER_RABBITMQ_PASSWORD",
            "LYRICS_WORKER_RABBITMQ_PASSWORD",
            "ESSENTIA_WORKER_RABBITMQ_PASSWORD",
            "JWT_SECRET",
            "JWT_ISSUER",
            "JWT_AUDIENCE",
            "CSRF_SECRET",
            "POSTGRES_EXPORTER_PASSWORD",
        };

        credentials.Keys.Should().Contain(expectedKeys);
    }

    [Fact]
    public void GenerateAllCredentials_never_generatesGarageApplicationKeys()
    {
        var sut = CreateSut();
        var features = new Builder.Models.FeatureSelection();

        var credentials = sut.GenerateAllCredentials(features);

        // Application S3 keys come exclusively from the garage-init merge (D6)
        credentials.Keys.Should().NotContain("GARAGE_S3_ACCESS_KEY");
        credentials.Keys.Should().NotContain("GARAGE_S3_SECRET_KEY");
    }

    [Fact]
    public void GenerateAllCredentials_monitoringEnabled_includesGrafanaAdminPassword()
    {
        var sut = CreateSut();
        var features = new Builder.Models.FeatureSelection();

        features.SetEnabled(Builder.Models.FeatureCatalog.Monitoring.Id, true);

        var credentials = sut.GenerateAllCredentials(features);

        credentials.Should().ContainKey("GRAFANA_ADMIN_PASSWORD");
    }

    [Fact]
    public void GenerateAllCredentials_monitoringDisabled_omitsGrafanaAdminPassword()
    {
        var sut = CreateSut();
        var features = new Builder.Models.FeatureSelection();

        var credentials = sut.GenerateAllCredentials(features);

        credentials.Should().NotContainKey("GRAFANA_ADMIN_PASSWORD");
    }
}
