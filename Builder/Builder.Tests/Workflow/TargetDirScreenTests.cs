using AwesomeAssertions;
using Builder.Models;
using Builder.Tests.Support;
using Builder.UI.Shell;
using Builder.UI.Shell.Screens;

namespace Builder.Tests.Workflow;

[Collection(nameof(StaticConsoleTests))]
public class TargetDirScreenTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "builder-target-tests", Guid.NewGuid().ToString("N"));

    public TargetDirScreenTests()
    {
        CreateSource(_root);
        TargetDirScreen.RepoRootOverride = () => _root;
    }

    [Fact]
    public void Run_detectedRepo_acceptsResolvedSourceAndTarget()
    {
        var keys = new FakeKeys().Enter().Enter().Enter();

        using var harness = new ShellHarness(keys);

        var outcome = TargetDirScreen.Run(harness.Shell);

        outcome.Should().Be(ScreenOutcome.Continue);
        harness.Context.InstallPath.Should().Be(_root);
        harness.Context.SourceRoot.Should().Be(_root);
        harness.Context.Config.InstallPath.Should().Be(_root);
        harness.Context.Config.SourceRoot.Should().Be(_root);
        harness.Output.Should().Contain("builds from here");
        harness.Output.Should().Contain("Docker-managed volumes");
    }

    [Fact]
    public void Run_repoNotDetected_resolvesQuotedRelativeSourceAndTargetFromLaunchDirectory()
    {
        var source = Path.Combine(_root, "checkout with spaces");
        CreateSource(source);
        TargetDirScreen.RepoRootOverride = () => null;
        var relativeSource = Path.GetRelativePath(Environment.CurrentDirectory, source);
        var target = Path.Combine(_root, "settings with spaces");
        var relativeTarget = Path.GetRelativePath(Environment.CurrentDirectory, target);

        var keys = new FakeKeys()
            .Press(ConsoleKey.U, 'u', ctrl: true).Text($"\"{relativeSource}\"").Enter()
            .Press(ConsoleKey.U, 'u', ctrl: true).Text(relativeTarget + Path.DirectorySeparatorChar).Enter()
            .Enter();

        using var harness = new ShellHarness(keys);

        var outcome = TargetDirScreen.Run(harness.Shell);

        outcome.Should().Be(ScreenOutcome.Continue);
        harness.Context.SourceRoot.Should().Be(source);
        harness.Context.InstallPath.Should().Be(target);
        harness.Output.Should().Contain("Source code").And.Contain("Setup settings");
        Directory.Exists(harness.Context.InstallPath).Should().BeFalse();
    }

    [Fact]
    public void Run_existingEnv_warnsAndPreservesContents()
    {
        var environmentPath = Path.Combine(_root, ".env");
        File.WriteAllText(environmentPath, "DB_PASSWORD=synthetic-existing-value");

        using var harness = new ShellHarness(new FakeKeys().Enter().Enter().Enter());

        var outcome = TargetDirScreen.Run(harness.Shell);

        outcome.Should().Be(ScreenOutcome.Continue);
        harness.Output.Should().Contain("already has an Alexandria configuration");
        File.ReadAllText(environmentPath).Should().Be("DB_PASSWORD=synthetic-existing-value");
        Directory.GetFiles(_root, ".alexandria-write-probe-*").Should().BeEmpty();
    }

    [Fact]
    public void Run_workingDirectoryChangesAfterLaunch_keepsCapturedRelativePathBase()
    {
        var keys = new FakeKeys();
        using var harness = new ShellHarness(keys);
        var target = Path.Combine(_root, "settings");
        var relativeSource = Path.GetRelativePath(harness.Context.LaunchDirectory, _root);
        var relativeTarget = Path.GetRelativePath(harness.Context.LaunchDirectory, target);

        keys.Press(ConsoleKey.U, 'u', ctrl: true).Text(relativeSource).Enter()
            .Press(ConsoleKey.U, 'u', ctrl: true).Text(relativeTarget).Enter().Enter();

        var originalDirectory = Environment.CurrentDirectory;

        try
        {
            Environment.CurrentDirectory = _root;

            TargetDirScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Continue);

            harness.Context.SourceRoot.Should().Be(_root);
            harness.Context.InstallPath.Should().Be(target);
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
        }
    }

    [Fact]
    public void Run_escapeDuringSource_preservesPartialDraftWithoutAcceptingIt()
    {
        var keys = new FakeKeys().Text("-unfinished").Press(ConsoleKey.Escape);
        using var harness = new ShellHarness(keys);

        TargetDirScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Back);

        harness.Context.SourcePathDraft.Should().Be(_root + "-unfinished");
        harness.Context.SourceRoot.Should().BeEmpty();

        keys.Backspaces("-unfinished".Length).Enter().Enter().Enter();

        TargetDirScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Continue);
        harness.Context.SourceRoot.Should().Be(_root);
    }

    [Fact]
    public void Run_escapeDuringTarget_preservesDraftAndLeavesTargetUntouched()
    {
        var target = Path.Combine(_root, "new settings");
        var keys = new FakeKeys().Enter()
            .Press(ConsoleKey.U, 'u', ctrl: true).Text(target).Press(ConsoleKey.Escape);

        using var harness = new ShellHarness(keys);

        TargetDirScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Back);

        harness.Context.TargetPathDraft.Should().Be(target);
        harness.Context.InstallPath.Should().BeEmpty();
        Directory.Exists(target).Should().BeFalse();

        keys.Enter().Enter().Enter();

        TargetDirScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Continue);
        harness.Context.InstallPath.Should().Be(target);
        Directory.Exists(target).Should().BeFalse();
    }

    [Fact]
    public void Run_escapeAtResolvedReview_doesNotCreateProbeOrDirectories()
    {
        var target = Path.Combine(_root, "absent", "settings");
        var before = Directory.GetFileSystemEntries(_root).Order().ToArray();
        var keys = new FakeKeys().Enter()
            .Press(ConsoleKey.U, 'u', ctrl: true).Text(target).Enter()
            .Press(ConsoleKey.Escape);

        using var harness = new ShellHarness(keys);

        TargetDirScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Back);

        harness.Context.TargetPathDraft.Should().Be(target);
        harness.Context.InstallPath.Should().BeEmpty();
        Directory.GetFileSystemEntries(_root).Order().Should().Equal(before);
    }

    [Fact]
    public void Run_erasedTargetShowsErrorAndRemainsEmptyAfterEscapeAndReentry()
    {
        var keys = new FakeKeys().Enter()
            .Press(ConsoleKey.U, 'u', ctrl: true).Enter().Press(ConsoleKey.Escape);

        using var harness = new ShellHarness(keys);

        TargetDirScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Back);
        harness.Context.TargetPathDraft.Should().BeEmpty();
        harness.Output.Should().Contain("Enter a folder path");

        var target = Path.Combine(_root, "new settings");
        keys.Enter().Text(target).Enter().Enter();

        TargetDirScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Continue);
        harness.Context.InstallPath.Should().Be(target);
    }

    [Fact]
    public void Run_missingSelectedAsset_canCorrectSourceWithoutRestartingWizard()
    {
        var incomplete = Path.Combine(_root, "incomplete");
        Directory.CreateDirectory(Path.Combine(incomplete, "Backend"));
        Directory.CreateDirectory(Path.Combine(incomplete, "Frontend"));

        var keys = new FakeKeys()
            .Press(ConsoleKey.U, 'u', ctrl: true).Text(incomplete).Enter()
            .Press(ConsoleKey.U, 'u', ctrl: true).Text(_root).Enter().Enter().Enter();

        using var harness = new ShellHarness(keys);

        TargetDirScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Continue);
        harness.Output.Should().Contain("Missing required source asset");
        harness.Context.SourceRoot.Should().Be(_root);
    }

    internal static void CreateSource(string root)
    {
        string[] files =
        [
            "Backend/Alexandria.Api/Dockerfile",
            "Backend/Alexandria.Api/Alexandria.Api.csproj",
            "Backend/Directory.Packages.props",
            "Backend/Directory.Build.props",
            "Backend/nuget.config",
            "Frontend/Dockerfile",
            "Frontend/package.json",
            "Frontend/pnpm-lock.yaml",
            "Frontend/pnpm-workspace.yaml",
            "Frontend/src/main.ts",
            "Frontend/nginx.conf",
            "nginx/Dockerfile",
            "nginx/nginx.conf",
            "nginx/gpg/arut.key",
            "scripts/garage-init/Dockerfile",
            "scripts/garage-init/init.sh",
            "scripts/garage-init/cors.json",
            "scripts/postgres/init/01-create-exporter-user.sh",
            "rabbitmq/definitions.template.json",
            "rabbitmq/rabbitmq.template.conf",
            "rabbitmq/enabled_plugins",
            "Backend/Alexandria.Workers.Document/Dockerfile",
            "Backend/Alexandria.Workers.Media/Dockerfile",
            "Backend/Alexandria.Workers.MediaTranspilation/Dockerfile",
            "Backend/Alexandria.Workers.Lyrics/Dockerfile",
            "Backend/Alexandria.Workers.Playlist/Dockerfile",
            "Backend/Alexandria.Workers.MediaMetadata/Dockerfile",
            "classificators/essentia/Dockerfile",
            "classificators/essentia/entrypoint.sh",
            "classificators/essentia/download-models.sh",
            "classificators/essentia/essentia_core.py",
            "classificators/essentia/classify.py",
            "classificators/essentia/worker.py",
            "classificators/essentia/models/models.sha256",
            "classificators/essentia/model_assets.py", "classificators/essentia/model-requirements.tsv",
            "grafana/provisioning/dashboards/dashboard.yml",
            "grafana/provisioning/datasources/prometheus.yml",
            "grafana/provisioning/datasources/loki.yml",
            "grafana/provisioning/dashboards/alexandria-overview.json",
            "grafana/provisioning/dashboards/alexandria-api.json",
            "grafana/provisioning/dashboards/alexandria-logs.json",
            "alloy/config.alloy",
            "loki/loki-config.yml",
        ];

        foreach (var relativePath in files)
        {
            var path = Path.Combine(root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "synthetic source fixture");
        }
    }

    public void Dispose()
    {
        TargetDirScreen.RepoRootOverride = null;
        Directory.Delete(_root, recursive: true);
    }
}
