using System.Text.Json;
using AwesomeAssertions;
using Builder.Catalog;
using Builder.Common;
using Builder.Configuration;
using Builder.Diagnostics;
using Builder.Docker;
using Builder.SystemCheck;
using NSubstitute;

namespace Builder.Tests.Diagnostics;

public sealed class VideoGpuDiagnosisTests : IDisposable
{
    private static readonly RenderNode Amd = new("/dev/dri/by-path/pci-0000:04:00.0-render", "0000:04:00.0", "AMD", "226:128", 110);
    private static readonly RenderNode Intel = new("/dev/dri/by-path/pci-0000:00:02.0-render", "0000:00:02.0", "Intel", "226:129", 110);

    private readonly string _folder = Directory.CreateTempSubdirectory("builder-diagnose-").FullName;
    private readonly ISystemChecker _checker = Substitute.For<ISystemChecker>();
    private readonly IDockerService _docker = Substitute.For<IDockerService>();

    public VideoGpuDiagnosisTests()
    {
        _checker.GetDockerContextError().Returns((string?)null);
        HasGpus(Amd, Intel);
        _docker.InspectImage(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new ProcessResult(0, "sha256:1", string.Empty));
        _docker.ProbeVideoGpuInImage(Arg.Any<string>(), Amd, Arg.Any<CancellationToken>()).Returns(Passes(Amd.Path));
        _docker.ProbeVideoGpuInImage(Arg.Any<string>(), Intel, Arg.Any<CancellationToken>())
            .Returns(Fails(Intel.Path, "No usable encoding profile found."));
        Builds(new ProcessResult(0, string.Empty, string.Empty));
        _docker.RemoveImage(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new ProcessResult(0, string.Empty, string.Empty));
        _docker.ProbeVideoGpus(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<Action<string>?>())
            .Returns(Passes(Amd.Path));
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Run_wiredGpuWorks_reportsEveryNodeAndTheInstalledWorker()
    {
        Install(Wired(Amd));

        var report = Diagnose();

        report.Setting.Should().Be("Encodes video on the AMD GPU at 0000:04:00.0");
        report.WorkerEncodes.Should().BeTrue();
        report.WiredGpuFails.Should().BeFalse();
        report.Incomplete.Should().BeFalse();
        report.Nodes.Should().Equal(
            new VideoGpuNodeReport(Amd, true, "Mesa test driver", true),
            new VideoGpuNodeReport(Intel, false, "No usable encoding profile found.", false));
        report.Notes.Should().BeEmpty();
        _docker.Received(1).ProbeVideoGpus(_folder, Arg.Is<IReadOnlyList<string>>(devices => devices.Single() == Amd.Path),
            Arg.Any<Action<string>?>());
    }

    [Fact]
    public void Run_installedWorkerCannotEncode_flagsTheWiredGpu()
    {
        Install(Wired(Amd));
        _docker.ProbeVideoGpus(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<Action<string>?>())
            .Returns(Fails(Amd.Path, "No permission to open the device."));

        var report = Diagnose();

        report.WorkerEncodes.Should().BeFalse();
        report.WorkerDetail.Should().Be("No permission to open the device.");
        report.WiredGpuFails.Should().BeTrue();
    }

    // Render nodes can renumber between boots, which leaves the device rule naming another one.
    [Fact]
    public void Run_deviceNumberChangedSinceSetup_asksForAnotherSetupRun()
    {
        Install(Wired(Amd with { DeviceNumber = "226:130" }));

        var report = Diagnose();

        report.Notes.Should().ContainSingle().Which.Should().Contain("device number or group changed");
    }

    [Fact]
    public void Run_wiredDeviceGone_saysSo()
    {
        Install(Wired(Amd));
        HasGpus(Intel);
        _docker.ProbeVideoGpus(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<Action<string>?>())
            .Returns(Fails(Amd.Path, "The device does not exist in this container."));

        var report = Diagnose();

        report.Setting.Should().Be("Encodes video on the device " + Amd.Path);
        report.Notes.Should().ContainSingle().Which.Should().Contain("was not found on this machine");
        report.WiredGpuFails.Should().BeTrue();
    }

    [Fact]
    public void Run_cpuInstallation_testsTheNodesButNoWorkerWiring()
    {
        Install(new Dictionary<string, string> { [VideoEncodingEnvironment.ModeKey] = "Cpu" });

        var report = Diagnose();

        report.Setting.Should().Be("Encodes video on the CPU");
        report.Nodes.Select(node => node.Encodes).Should().Equal(true, false);
        report.WorkerEncodes.Should().BeNull();
        report.WiredGpuFails.Should().BeFalse();
        report.Notes.Should().ContainSingle().Which.Should().Contain("does not use it");
        _docker.DidNotReceive().ProbeVideoGpus(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<Action<string>?>());
    }

    [Fact]
    public void Run_imageNotBuilt_offersTheInstallationsSourceWithoutBuilding()
    {
        Install(Wired(Amd));
        ImageMissing();

        var report = Diagnose();

        report.BuildSource.Should().Be(_folder);
        report.Nodes.Should().HaveCount(2).And.OnlyContain(node => node.Encodes == null);
        report.Notes.Should().ContainSingle().Which.Should().Contain("its worker was not checked");
        report.WiredGpuFails.Should().BeFalse();
        AssertNothingProbed();
        AssertNothingBuilt();
    }

    // The temporary image has its own name, so the installation's is never replaced.
    [Fact]
    public void Run_imageNotBuiltAndBuildAllowed_testsATemporaryImageAndRemovesIt()
    {
        Install(Wired(Amd));
        ImageMissing();
        var output = new List<string>();

        var report = Diagnose(build: true, output: output.Add);

        report.Nodes.Select(node => node.Encodes).Should().Equal(true, false);
        report.BuildSource.Should().BeNull();
        report.WorkerEncodes.Should().BeNull();
        report.Notes.Should().Contain(note => note.Contains("temporary worker image"));
        output.Should().HaveCount(2);
        output[0].Should().Contain("Building the transcoding worker from " + _folder);
        output[1].Should().Be("#1 building");
        Received.InOrder(() =>
        {
            _docker.BuildTranscoderImage(_folder, VideoGpuDiagnosis.TemporaryImage, Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>());
            _docker.ProbeVideoGpuInImage(VideoGpuDiagnosis.TemporaryImage, Amd, Arg.Any<CancellationToken>());
            _docker.ProbeVideoGpuInImage(VideoGpuDiagnosis.TemporaryImage, Intel, Arg.Any<CancellationToken>());
            _docker.RemoveImage(VideoGpuDiagnosis.TemporaryImage, Arg.Any<CancellationToken>());
        });
        _docker.DidNotReceive().ProbeVideoGpus(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<Action<string>?>());
    }

    [Fact]
    public void Run_buildFails_reportsItsErrorAndTestsNothing()
    {
        Install(Wired(Amd));
        ImageMissing();
        Builds(new ProcessResult(1, string.Empty, string.Empty), "ERROR: failed to solve: no network");

        var report = Diagnose(build: true);

        report.BuildFailed.Should().BeTrue();
        report.Notes.Should().Contain(note => note.EndsWith("ERROR: failed to solve: no network"));
        AssertNothingProbed();
        _docker.DidNotReceive().RemoveImage(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Run_testCancelled_stillRemovesTheTemporaryImage()
    {
        Install(Wired(Amd));
        ImageMissing();
        _docker.ProbeVideoGpuInImage(Arg.Any<string>(), Amd, Arg.Any<CancellationToken>())
            .Returns<ProcessResult>(_ => throw new OperationCanceledException());

        var run = () => Diagnose(build: true);

        run.Should().Throw<OperationCanceledException>();
        _docker.Received(1).RemoveImage(VideoGpuDiagnosis.TemporaryImage, CancellationToken.None);
    }

    [Fact]
    public void Run_temporaryImageCannotBeRemoved_saysHowToRemoveIt()
    {
        Install(Wired(Amd));
        ImageMissing();
        _docker.RemoveImage(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new ProcessResult(1, string.Empty, "in use"));

        var report = Diagnose(build: true);

        report.Notes.Should().Contain(note => note.Contains("docker image rm " + VideoGpuDiagnosis.TemporaryImage));
    }

    // A source older than GPU support has no probe to run, so the Builder's own checkout is used.
    [Fact]
    public void Run_installationSourceWithoutTheProbe_buildsFromTheCheckout()
    {
        var checkout = Directory.CreateDirectory(Path.Combine(_folder, "checkout")).FullName;
        AddWorkerSource(checkout);
        Install(Wired(Amd), source: Path.Combine(_folder, "old"));
        ImageMissing();

        var report = Diagnose(checkout: checkout);

        report.BuildSource.Should().Be(checkout);
    }

    [Fact]
    public void Run_noUsableSource_saysTheGpusCouldNotBeTested()
    {
        var report = Diagnose(build: true, checkout: Path.Combine(_folder, "missing"));

        report.BuildSource.Should().BeNull();
        report.Notes.Should().Contain(note => note.Contains("No Alexandria source"));
        report.Incomplete.Should().BeTrue();
        AssertNothingBuilt();
    }

    // Without the installed image the worker is not probed, so the wired GPU's own test decides.
    [Fact]
    public void Run_wiredGpuFailsInTheTemporaryImage_flagsTheWiredGpu()
    {
        Install(Wired(Intel));
        ImageMissing();

        var report = Diagnose(build: true);

        report.WiredGpuFails.Should().BeTrue();
        report.Incomplete.Should().BeFalse();
    }

    [Fact]
    public void Run_wiredGpuEncodesInTheTemporaryImage_passes()
    {
        Install(Wired(Amd));
        ImageMissing();

        var report = Diagnose(build: true);

        report.WiredGpuFails.Should().BeFalse();
        report.Incomplete.Should().BeFalse();
    }

    [Fact]
    public void Run_dockerContextBroken_isIncomplete()
    {
        _checker.GetDockerContextError().Returns("The Docker context 'remote' cannot be reached.");

        var report = Diagnose(build: true);

        report.Incomplete.Should().BeTrue();
        AssertNothingProbed();
    }

    [Fact]
    public void Run_noNodeTestRan_isIncomplete()
    {
        Install(new Dictionary<string, string> { [VideoEncodingEnvironment.ModeKey] = "Cpu" });
        _docker.ProbeVideoGpuInImage(Arg.Any<string>(), Arg.Any<RenderNode>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(125, string.Empty, "docker: Error response from daemon: bad rule"));

        var report = Diagnose();

        report.Incomplete.Should().BeTrue();
    }

    [Fact]
    public void Run_dockerCannotBeAsked_reportsItsReasonWithoutBuilding()
    {
        Install(Wired(Amd));
        _docker.InspectImage(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, string.Empty, "error: Cannot connect to the Docker daemon"));

        var report = Diagnose(build: true);

        report.Notes.Should().ContainSingle().Which.Should().Contain("Cannot connect to the Docker daemon");
        report.Incomplete.Should().BeTrue();
        AssertNothingProbed();
        AssertNothingBuilt();
    }

    [Fact]
    public void Run_probeNeverRan_leavesTheNodeUntested()
    {
        Install(new Dictionary<string, string> { [VideoEncodingEnvironment.ModeKey] = "Cpu" });
        _docker.ProbeVideoGpuInImage(Arg.Any<string>(), Amd, Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(125, string.Empty, "docker: Error response from daemon: bad rule"));

        var node = Diagnose().Nodes[0];

        node.Encodes.Should().BeNull();
        node.Detail.Should().Be("The test did not run: docker: Error response from daemon: bad rule");
    }

    [Fact]
    public void Run_noInstallation_testsTheGpusWithABuildFromTheCheckout()
    {
        AddWorkerSource(_folder);

        var report = Diagnose(build: true, checkout: _folder);

        report.Setting.Should().BeNull();
        report.Nodes.Select(node => node.Encodes).Should().Equal(true, false);
        report.Notes.Should().Contain("There is no Alexandria installation in this folder.");
        _docker.DidNotReceive().InspectImage(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // A worker built before GPU support would take --probe-gpu for a normal start.
    [Fact]
    public void Run_settingsWithoutAnEncoder_doesNotRunTheOldWorker()
    {
        Install(new Dictionary<string, string>());

        var report = Diagnose(build: true);

        report.Notes.Should().Contain(note => note.Contains("no video encoder yet"));
        _docker.DidNotReceive().InspectImage(Arg.Any<string>(), Arg.Any<CancellationToken>());
        _docker.DidNotReceive().ProbeVideoGpuInImage(Arg.Is<string>(image => image != VideoGpuDiagnosis.TemporaryImage),
            Arg.Any<RenderNode>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Run_streamingNotInstalled_saysNoVideoIsEncoded()
    {
        Install(new Dictionary<string, string>(), streaming: false);

        var report = Diagnose();

        report.Notes.Should().ContainSingle().Which.Should().Contain("encodes no video");
        AssertNothingProbed();
    }

    [Fact]
    public void Run_noGpu_saysWhy()
    {
        HasGpus();
        Install(new Dictionary<string, string> { [VideoEncodingEnvironment.ModeKey] = "Cpu" });

        var report = Diagnose();

        report.Nodes.Should().BeEmpty();
        report.Notes.Should().Contain("No AMD or Intel GPU was found on this machine.");
    }

    [Fact]
    public void Run_noGpuAndNoImage_buildsNothing()
    {
        HasGpus();
        AddWorkerSource(_folder);

        var report = Diagnose(build: true, checkout: _folder);

        report.BuildSource.Should().BeNull();
        AssertNothingBuilt();
    }

    [Fact]
    public void Render_report_namesEveryNodeItsOutcomeAndTheNotes()
    {
        Install(Wired(Amd));

        var text = VideoGpuReportText.Render(Diagnose() with { Notes = ["Check this."] });

        text.Should().Contain("Encodes video on the AMD GPU at 0000:04:00.0")
            .And.Contain("Worker as installed: Encodes with Mesa test driver")
            .And.Contain("AMD GPU at 0000:04:00.0 (in use)")
            .And.Contain(Intel.Path)
            .And.Contain("Cannot encode: No usable encoding profile found.")
            .And.Contain("- Check this.");
    }

    private VideoGpuReport Diagnose(bool build = false, string? checkout = null, Action<string>? output = null) =>
        new VideoGpuDiagnosis(_checker, _docker).Run(_folder, checkout, build, output);

    private void HasGpus(params RenderNode[] gpus) =>
        _checker.GetSystemResources().Returns(new SystemResources { VideoGpus = gpus });

    private void ImageMissing() => _docker.InspectImage(Arg.Any<string>(), Arg.Any<CancellationToken>())
        .Returns(new ProcessResult(1, string.Empty, "Error response from daemon: No such image: test-transpilation-worker:latest"));

    private void Builds(ProcessResult result, string line = "#1 building") =>
        _docker.BuildTranscoderImage(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                call.Arg<Action<string>?>()?.Invoke(line);

                return result;
            });

    private static void AddWorkerSource(string root)
    {
        var worker = Directory.CreateDirectory(Path.Combine(root, "Backend", "Alexandria.Workers.MediaTranspilation")).FullName;
        File.WriteAllText(Path.Combine(worker, "Dockerfile"), string.Empty);
        File.WriteAllText(Path.Combine(worker, "GpuProbeCommand.cs"), string.Empty);
    }

    private void AssertNothingProbed()
    {
        _docker.DidNotReceive().ProbeVideoGpuInImage(Arg.Any<string>(), Arg.Any<RenderNode>(), Arg.Any<CancellationToken>());
        _docker.DidNotReceive().ProbeVideoGpus(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<Action<string>?>());
    }

    private void AssertNothingBuilt() => _docker.DidNotReceive()
        .BuildTranscoderImage(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>());

    private static Dictionary<string, string> Wired(RenderNode node) => new()
    {
        [VideoEncodingEnvironment.ModeKey] = "Auto",
        [VideoEncodingEnvironment.DeviceKey] = node.Path,
        [VideoEncodingEnvironment.GroupKey] = node.GroupId.ToString(),
        [VideoEncodingEnvironment.DeviceNumberKey] = node.DeviceNumber,
    };

    private static ProcessResult Passes(string device) =>
        new(0, $"[{{\"device\":\"{device}\",\"usable\":true,\"driver\":\"Mesa test driver\"}}]", string.Empty);

    private static ProcessResult Fails(string device, string detail) =>
        new(1, $"[{{\"device\":\"{device}\",\"usable\":false,\"detail\":\"{detail}\"}}]", string.Empty);

    private void Install(Dictionary<string, string> environment, bool streaming = true, string? source = null)
    {
        if (source is null) AddWorkerSource(_folder);

        var config = new InstallationConfig
        {
            SchemaVersion = 2,
            ProjectName = "test",
            InstallPath = _folder,
            SourceRoot = source ?? _folder,
        };
        config.Features.SetEnabled(FeatureCatalog.AdaptiveStreaming.Id, streaming);

        File.WriteAllText(Path.Combine(_folder, "alexandria-config.json"),
            JsonSerializer.Serialize(config, AlexandriaJsonContext.Default.InstallationConfig));
        File.WriteAllLines(Path.Combine(_folder, ".env"), environment.Select(pair => $"{pair.Key}={pair.Value}"));
    }
}
