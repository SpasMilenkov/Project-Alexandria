using System.Text.Json;
using AwesomeAssertions;
using Builder.Catalog;
using Builder.Common;
using Builder.Configuration;
using Builder.Docker;
using Builder.SystemCheck;
using Builder.Tests.Support;
using Builder.UI.Shell.Screens;
using NSubstitute;

namespace Builder.Tests.UI.Screens;

[Collection(nameof(StaticConsoleTests))]
public sealed class VideoGpuScreenTests : IDisposable
{
    private static readonly RenderNode Amd = new("/dev/dri/by-path/pci-0000:04:00.0-render", "0000:04:00.0", "AMD", "226:128", 110);
    private static readonly RenderNode Intel = new("/dev/dri/by-path/pci-0000:00:02.0-render", "0000:00:02.0", "Intel", "226:129", 110);

    private readonly string _folder = Directory.CreateTempSubdirectory("builder-gpu-screen-").FullName;
    private readonly ISystemChecker _checker = Substitute.For<ISystemChecker>();
    private readonly IDockerService _docker = Substitute.For<IDockerService>();

    public VideoGpuScreenTests()
    {
        var config = new InstallationConfig
        {
            SchemaVersion = 2,
            ProjectName = "test",
            InstallPath = _folder,
            SourceRoot = _folder,
        };
        config.Features.SetEnabled(FeatureCatalog.AdaptiveStreaming.Id, true);

        File.WriteAllText(Path.Combine(_folder, "alexandria-config.json"),
            JsonSerializer.Serialize(config, AlexandriaJsonContext.Default.InstallationConfig));
        File.WriteAllText(Path.Combine(_folder, ".env"), "VIDEO_ENCODING_MODE=Cpu\n");

        _checker.GetDockerContextError().Returns((string?)null);
        _docker.InspectImage(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new ProcessResult(0, "sha256:1", string.Empty));
        _docker.ProbeVideoGpuInImage(Arg.Any<string>(), Arg.Any<RenderNode>(), Arg.Any<CancellationToken>()).Returns(call =>
            new ProcessResult(0, $"[{{\"device\":\"{call.Arg<RenderNode>().Path}\",\"usable\":true}}]", string.Empty));
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Run_escapeOnTheFolder_testsNothing()
    {
        using var h = new ShellHarness(new FakeKeys().Press(ConsoleKey.Escape));
        h.Context.TargetPathDraft = _folder;

        VideoGpuScreen.Run(h.Shell, _checker, _docker);

        _checker.DidNotReceive().GetSystemResources();
        h.Shell.TakeEscape().Should().BeFalse();
    }

    [Fact]
    public void Run_severalUsableGpus_keepsTheOnePickedForTheNextSetupRun()
    {
        HasGpus(Amd, Intel);
        using var h = new ShellHarness(new WaitOnlyKeys(new FakeKeys()
            .Enter()                 // folder
            .Down().Down().Enter()   // leave it, AMD, Intel
            .Enter()));              // confirmation
        h.Context.TargetPathDraft = _folder;

        VideoGpuScreen.Run(h.Shell, _checker, _docker);

        h.Context.ChosenVideoGpu.Should().Be(Intel.Path);
        h.Context.TargetPathDraft.Should().Be(_folder);
        h.Output.Should().Contain("Not applied yet");
    }

    [Fact]
    public void Run_severalUsableGpus_leftAsTheyAre_picksNothing()
    {
        HasGpus(Amd, Intel);
        using var h = new ShellHarness(new WaitOnlyKeys(new FakeKeys().Enter().Enter()));
        h.Context.TargetPathDraft = _folder;

        VideoGpuScreen.Run(h.Shell, _checker, _docker);

        h.Context.ChosenVideoGpu.Should().BeNull();
    }

    [Fact]
    public void Run_oneGpu_showsTheReportAndChecksAgainOnR()
    {
        HasGpus(Amd);
        using var h = new ShellHarness(new WaitOnlyKeys(new FakeKeys().Enter().Press(ConsoleKey.R, 'r').Enter()));
        h.Context.TargetPathDraft = _folder;

        VideoGpuScreen.Run(h.Shell, _checker, _docker);

        _checker.Received(2).GetSystemResources();
        h.Output.Should().Contain("AMD GPU at 0000:04:00.0").And.Contain("Encodes video on the CPU");
        h.Context.ChosenVideoGpu.Should().BeNull();
    }

    [Theory]
    [InlineData(ConsoleKey.Enter, 1)]
    [InlineData(ConsoleKey.Escape, 0)]
    public void Run_imageMissing_buildsOnlyWhenAccepted(ConsoleKey answer, int builds)
    {
        HasGpus(Amd);
        var worker = Directory.CreateDirectory(Path.Combine(_folder, "Backend", "Alexandria.Workers.MediaTranspilation")).FullName;
        File.WriteAllText(Path.Combine(worker, "Dockerfile"), string.Empty);
        File.WriteAllText(Path.Combine(worker, "GpuProbeCommand.cs"), string.Empty);
        _docker.InspectImage(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, string.Empty, "No such image: test-transpilation-worker"));
        _docker.BuildTranscoderImage(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, string.Empty, string.Empty));
        _docker.RemoveImage(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new ProcessResult(0, string.Empty, string.Empty));
        var keys = new FakeKeys().Enter().Press(answer);

        if (builds > 0) keys.Enter(); // leave the tested report

        using var h = new ShellHarness(new WaitOnlyKeys(keys));
        h.Context.TargetPathDraft = _folder;

        VideoGpuScreen.Run(h.Shell, _checker, _docker);

        h.Output.Should().Contain("Build a worker image to test with?");
        _docker.Received(builds).BuildTranscoderImage(_folder, Arg.Any<string>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>());
    }

    private void HasGpus(params RenderNode[] gpus) =>
        _checker.GetSystemResources().Returns(new SystemResources { VideoGpus = gpus });
}
