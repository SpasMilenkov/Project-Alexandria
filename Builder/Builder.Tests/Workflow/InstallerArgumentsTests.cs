using System.Text.Json;
using AwesomeAssertions;
using Builder.Configuration;
using Builder.Workflow;

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

    // Diagnosis reads the folder itself, so unreadable settings become part of its report
    // instead of stopping at the arguments.
    [Fact]
    public void Apply_diagnoseGpuWithSettings_selectsTheFolderWithoutInspectingIt()
    {
        var root = Directory.CreateTempSubdirectory("builder diagnose ").FullName;

        try
        {
            File.WriteAllText(Path.Combine(root, "alexandria-config.json"), "not json");
            var context = new InstallationContext { LaunchDirectory = Path.GetTempPath() };

            InstallerArguments.Apply(["--diagnose", "gpu", "--settings", Path.GetFileName(root)], context).Should().BeTrue();

            context.DiagnoseGpu.Should().BeTrue();
            context.InstallPath.Should().Be(root);
            context.Cancellation.Dispose();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Apply_diagnoseGpuInAnInstallationFolder_usesIt()
    {
        var root = Directory.CreateTempSubdirectory("builder diagnose ").FullName;

        try
        {
            File.WriteAllText(Path.Combine(root, "alexandria-config.json"), "{}");
            var context = new InstallationContext { LaunchDirectory = root };

            InstallerArguments.Apply(["--diagnose", "gpu"], context).Should().BeTrue();

            context.InstallPath.Should().Be(root);
            context.Cancellation.Dispose();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("--diagnose")]
    [InlineData("--diagnose", "network")]
    [InlineData("--diagnose", "gpu", "--settings")]
    public void Apply_unknownDiagnosis_isRefused(params string[] arguments)
    {
        var context = new InstallationContext();
        var action = () => InstallerArguments.Apply(arguments, context);

        action.Should().Throw<ArgumentException>();
        context.DiagnoseGpu.Should().BeFalse();
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
