using System.Text.Json;
using AwesomeAssertions;
using Builder.Models;

namespace Builder.Tests.Models;

public class InstallationConfigJsonTests
{
    private static InstallationConfig CreateSut() => new()
    {
        Features = new FeatureSelection(),
        Ports = new Dictionary<string, int> { ["HTTP_PORT"] = 8080, ["HTTPS_PORT"] = 8443 },
        OriginalPorts = new Dictionary<string, int> { ["HTTP_PORT"] = 80 },
        Credentials = new Dictionary<string, string> { ["DB_PASSWORD"] = "secret" },
        InstallPath = "/opt/alexandria",
        SourceRoot = "/home/user/repos/Project-Alexandria",
        InstalledAt = new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero),
    };

    [Fact]
    public void RoundTrip_preservesPortsCredentialsAndPaths()
    {
        var config = CreateSut();

        config.Features.SetEnabled(FeatureCatalog.Monitoring.Id, true);

        var json = JsonSerializer.Serialize(config, AlexandriaJsonContext.Default.InstallationConfig);
        var restored = JsonSerializer.Deserialize(json, AlexandriaJsonContext.Default.InstallationConfig);

        restored.Should().NotBeNull();
        restored!.Ports.Should().BeEquivalentTo(config.Ports);
        restored.OriginalPorts.Should().BeEquivalentTo(config.OriginalPorts);
        restored.Credentials.Should().BeEquivalentTo(config.Credentials);
        restored.InstallPath.Should().Be(config.InstallPath);
        restored.SourceRoot.Should().Be(config.SourceRoot);
        restored.InstalledAt.Should().Be(config.InstalledAt);
        restored.Features.IsEnabled(FeatureCatalog.Monitoring.Id).Should().BeTrue();
    }

    [Fact]
    public void GarageCredentialSet_roundTrip_preservesAllSixKeys()
    {
        var credentials = new GarageCredentialSet(
            "GK-access", "s3cret", "GK-preview", "p-secret", "GK-streaming", "s-secret");

        var json = JsonSerializer.Serialize(credentials, AlexandriaJsonContext.Default.GarageCredentialSet);
        var restored = JsonSerializer.Deserialize(json, AlexandriaJsonContext.Default.GarageCredentialSet);

        restored.Should().Be(credentials);
    }

    [Fact]
    public void GarageCredentialSet_isComplete_requiresAllKeysPresent()
    {
        new GarageCredentialSet("a", "b", "c", "d", "e", "f").IsComplete.Should().BeTrue();

        new GarageCredentialSet("a", "b", "c", "d", "e", "").IsComplete.Should().BeFalse();
        new GarageCredentialSet("", "b", "c", "d", "e", "f").IsComplete.Should().BeFalse();
    }
}
