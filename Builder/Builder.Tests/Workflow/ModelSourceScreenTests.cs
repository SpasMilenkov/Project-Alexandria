using AwesomeAssertions;
using Builder.Models;
using Builder.Tests.Support;
using Builder.UI.Shell;
using Builder.UI.Shell.Screens;

namespace Builder.Tests.Workflow;

[Collection(nameof(StaticConsoleTests))]
public sealed class ModelSourceScreenTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "builder-model-screen", Guid.NewGuid().ToString("N"));

    private void Configure(ShellHarness harness, bool localFiles = false)
    {
        var assets = Path.Combine(_root, "source", "classificators", "essentia");
        var models = Path.Combine(assets, "models");
        Directory.CreateDirectory(models);
        File.WriteAllText(Path.Combine(models, "models.sha256"), new string('a', 64) + "  common.pb\n");
        File.WriteAllText(Path.Combine(assets, "model-requirements.tsv"), "shared common.pb 4 test/common.pb\n");

        if (localFiles) File.WriteAllText(Path.Combine(models, "common.pb"), "test");

        harness.Context.SourceRoot = Path.Combine(_root, "source");
        harness.Context.InstallPath = Path.Combine(_root, "settings");
        harness.Context.Features = new FeatureSelection();
        harness.Context.Features.SetAudioBackbone("effnet");
        harness.Context.Features.SetEnabled(FeatureCatalog.Autotagging.Id, true);
    }

    [Fact]
    public void Run_analysisOff_skipsModelsWithoutReadingSourceOrPrompting()
    {
        using var harness = new ShellHarness(new FakeKeys());
        harness.Context.Features = new FeatureSelection();

        ModelSourceScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Continue);

        harness.Output.Should().BeEmpty();
        Directory.Exists(_root).Should().BeFalse();
    }

    [Fact]
    public void Run_defaultOffline_missingFilesBlocksUntilBackWithoutEnablingDownloads()
    {
        using var harness = new ShellHarness(new FakeKeys().Enter().Enter().Press(ConsoleKey.Escape));
        Configure(harness);

        ModelSourceScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Back);

        harness.Context.Config.AllowModelDownloads.Should().BeFalse();
        harness.Output.Should().Contain("Missing 1 selected model files");
        Directory.Exists(harness.Context.InstallPath).Should().BeFalse();
    }

    [Fact]
    public void Run_downloadExplicitlySelected_allowsMissingAssetsWithoutStartingTransfer()
    {
        using var harness = new ShellHarness(new FakeKeys().Press(ConsoleKey.DownArrow).Enter());
        Configure(harness);

        ModelSourceScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Continue);

        harness.Context.Config.AllowModelDownloads.Should().BeTrue();
        harness.Output.Should().Contain("essentia.upf.edu").And.Contain("final review");
        Directory.Exists(harness.Context.InstallPath).Should().BeFalse();
    }

    [Fact]
    public void Run_escapeFromChoice_preservesPreviousConsent()
    {
        using var harness = new ShellHarness(new FakeKeys().Press(ConsoleKey.Escape));
        Configure(harness);
        harness.Context.Config.AllowModelDownloads = true;

        ModelSourceScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Back);

        harness.Context.Config.AllowModelDownloads.Should().BeTrue();
    }

    [Fact]
    public void Run_switchToOffline_revokesPreviousDownloadConsent()
    {
        using var harness = new ShellHarness(new FakeKeys().Press(ConsoleKey.UpArrow).Enter().Enter());
        Configure(harness, localFiles: true);
        harness.Context.Config.AllowModelDownloads = true;

        ModelSourceScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Continue);

        harness.Context.Config.AllowModelDownloads.Should().BeFalse();
        harness.Context.Config.ModelSourceDirectory.Should().Be(Path.Combine(_root, "source", "classificators", "essentia", "models"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
