using System.Text.Json;
using AwesomeAssertions;
using Builder.Models;
using Builder.Services;

namespace Builder.Tests.Services;

public class ConfigurationPathTests : IDisposable
{
    private readonly string _target = Path.Combine(Path.GetTempPath(), $"builder-config-path-{Guid.NewGuid():N}");

    [Fact]
    public void LoadConfiguration_legacyRelativeAndHomePaths_normalizesFromCapturedLaunchDirectory()
    {
        Directory.CreateDirectory(_target);
        var config = new InstallationConfig { SchemaVersion = 1, InstallPath = "settings with spaces", SourceRoot = "~/alexandria-source" };
        File.WriteAllText(Path.Combine(_target, "alexandria-config.json"),
            JsonSerializer.Serialize(config, AlexandriaJsonContext.Default.InstallationConfig));
        var sut = new ConfigurationService(new TemplateService("Local"), _target);

        var restored = sut.LoadConfiguration(_target);

        restored.Should().NotBeNull();
        restored!.InstallPath.Should().Be(Path.Combine(_target, "settings with spaces"));
        restored.SourceRoot.Should().Be(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "alexandria-source"));
    }

    [Fact]
    public void LoadConfiguration_erasedLegacyPath_reportsErrorWithoutRewritingMetadata()
    {
        Directory.CreateDirectory(_target);
        var config = new InstallationConfig { SchemaVersion = 1, InstallPath = "", SourceRoot = "/source" };
        var original = JsonSerializer.Serialize(config, AlexandriaJsonContext.Default.InstallationConfig);
        var metadata = Path.Combine(_target, "alexandria-config.json");
        File.WriteAllText(metadata, original);

        var action = () => new ConfigurationService(new TemplateService("Local")).LoadConfiguration(_target);

        action.Should().Throw<InvalidOperationException>().WithMessage("*paths are invalid*");
        File.ReadAllText(metadata).Should().Be(original);
    }

    public void Dispose()
    {
        if (Directory.Exists(_target))
        {
            Directory.Delete(_target, recursive: true);
        }
    }
}
