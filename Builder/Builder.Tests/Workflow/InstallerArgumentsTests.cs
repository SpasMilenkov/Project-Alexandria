using AwesomeAssertions;
using Builder.Models;
using Builder.Services;
using Builder.Workflow;
using System.Text.Json;

namespace Builder.Tests.Workflow;

public class InstallerArgumentsTests
{
    [Fact]
    public void Apply_existingSettings_selectsSavedSourceBeforeDirectoryScreen()
    {
        var root = Path.Combine(Path.GetTempPath(), "builder arguments " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var saved = new InstallationConfig
            {
                ProjectName = "alexandria-existing",
                InstallPath = root,
                SourceRoot = Path.Combine(root, "permanent source"),
            };
            File.WriteAllText(Path.Combine(root, "alexandria-config.json"),
                JsonSerializer.Serialize(saved, AlexandriaJsonContext.Default.InstallationConfig));
            var context = new InstallationContext { LaunchDirectory = Path.GetTempPath() };

            InstallerArguments.Apply(["--settings", Path.GetFileName(root)], context).Should().BeTrue();

            context.InstallPath.Should().Be(root);
            context.SourceRoot.Should().Be(saved.SourceRoot);
            context.InspectedTarget.Should().BeNull();
            context.DeploymentStarted.Should().BeFalse();
            context.Cancellation.Dispose();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("--settings")]
    [InlineData("--unexpected")]
    public void Apply_invalidArguments_stopsBeforeWizard(string argument)
    {
        var context = new InstallationContext();
        var action = () => InstallerArguments.Apply([argument], context);

        action.Should().Throw<ArgumentException>();
        context.DeploymentStarted.Should().BeFalse();
        context.Cancellation.Dispose();
    }

    [Fact]
    public void Apply_help_doesNotSelectOrCreateSettings()
    {
        var context = new InstallationContext();

        InstallerArguments.Apply(["--help"], context).Should().BeFalse();
        context.InstallPath.Should().BeEmpty();
        context.Cancellation.Dispose();
    }
}
