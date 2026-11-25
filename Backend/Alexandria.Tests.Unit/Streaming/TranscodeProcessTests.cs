using System.Text.Json;
using Alexandria.Common;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Exceptions.Transpilation;
using Alexandria.Common.Settings;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Streaming;
using Alexandria.Services.Streaming.VideoEncoding;
using Alexandria.Tests.Unit.Common;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Alexandria.Tests.Unit.Streaming;

public class TranscodeProcessTests
{
    private static readonly TimeSpan NoLimit = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan FastPoll = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan GoneWithin = TimeSpan.FromSeconds(2);

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly RecordingLogger<TranscodeProcessTests> _logger = new();
    private readonly Guid _jobId = Guid.NewGuid();

    private static void RequireLinux() =>
        Assert.SkipUnless(OperatingSystem.IsLinux(), "These tests spawn sh and inspect /proc.");

    private static bool OnPath(string binary) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
        .Split(Path.PathSeparator)
        .Any(dir => File.Exists(Path.Combine(dir, binary)));

    private TranscodeRequest Request(string script, string expectedOutput = "/nonexistent/output") =>
        new(_jobId, "input", $"-c \"{script}\"", "test pass", expectedOutput);

    private Task RunAsync(TranscodeRequest request, CancellationToken ct, TimeSpan? maxDuration = null) =>
        TranscodeProcess.RunAsync("sh", request, maxDuration ?? NoLimit, FastPoll, _unitOfWork, _logger, ct);

    private void StatusIs(JobStatus status) =>
        _unitOfWork.Jobs.GetStatusAsync(_jobId, Arg.Any<CancellationToken>()).Returns(status);

    [Fact]
    public async Task RunAsync_jobCancellationRequested_killsToolAndThrowsCancelled()
    {
        RequireLinux();
        StatusIs(JobStatus.CancellationRequested);
        var duration = SleepProbe.UniqueSleepDuration(60);

        var act = () => RunAsync(
            Request($"sleep {duration} & sleep {duration}; wait"), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<TranspilationCancelledException>();
        (await SleepProbe.SleepStopsWithinAsync(duration, GoneWithin)).Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_hostCancelled_killsToolAndThrowsOperationCanceled()
    {
        RequireLinux();
        StatusIs(JobStatus.Processing);
        var duration = SleepProbe.UniqueSleepDuration(60);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(300));

        var act = () => RunAsync(Request($"sleep {duration} & sleep {duration}; wait"), cts.Token);

        // The handler tells a shutdown from a job cancel by this type, and only releases the claim on it.
        var thrown = await act.Should().ThrowAsync<OperationCanceledException>();
        thrown.Which.Should().NotBeOfType<TranspilationCancelledException>();
        (await SleepProbe.SleepStopsWithinAsync(duration, GoneWithin)).Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_statusPollFails_keepsWatchingAndStillCancels()
    {
        RequireLinux();
        var polls = 0;
        _unitOfWork.Jobs.GetStatusAsync(_jobId, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            polls++;
            return polls == 1
                ? Task.FromException<JobStatus>(new InvalidOperationException("database blip"))
                : Task.FromResult(JobStatus.CancellationRequested);
        });
        var duration = SleepProbe.UniqueSleepDuration(60);

        var act = () => RunAsync(Request($"sleep {duration}"), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<TranspilationCancelledException>();
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Message.Contains("will try again"));
        (await SleepProbe.SleepStopsWithinAsync(duration, GoneWithin)).Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_toolExits_watchdogHasFinishedBeforeReturn()
    {
        RequireLinux();
        var directory = Directory.CreateTempSubdirectory("transcode-process-test-").FullName;
        var output = Path.Combine(directory, "manifest.mpd");
        var polls = 0;
        var inFlight = 0;

        // A slow query, so the watchdog is very likely mid-poll when the tool exits.
        _unitOfWork.Jobs.GetStatusAsync(_jobId, Arg.Any<CancellationToken>()).Returns(async call =>
        {
            Interlocked.Increment(ref polls);
            Interlocked.Increment(ref inFlight);
            try
            {
                await Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>());
                return JobStatus.Processing;
            }
            finally
            {
                // A real query takes a moment to unwind, which is what an unawaited watchdog races.
                await Task.Delay(100, CancellationToken.None);
                Interlocked.Decrement(ref inFlight);
            }
        });

        try
        {
            await RunAsync(Request($"sleep 0.3; touch '{output}'", output), TestContext.Current.CancellationToken);

            Volatile.Read(ref polls).Should().BeGreaterThan(0);
            Volatile.Read(ref inFlight).Should().Be(0);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_overTimeLimit_killsToolAndThrowsTimeout()
    {
        RequireLinux();
        StatusIs(JobStatus.Processing);
        var duration = SleepProbe.UniqueSleepDuration(60);

        var act = () => RunAsync(
            Request($"sleep {duration}"),
            TestContext.Current.CancellationToken,
            TimeSpan.FromMilliseconds(300));

        await act.Should().ThrowAsync<ExternalProcessTimeoutException>();
        (await SleepProbe.SleepStopsWithinAsync(duration, GoneWithin)).Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_toolFails_throwsWithExitCodeAndError()
    {
        RequireLinux();
        StatusIs(JobStatus.Processing);

        var act = () => RunAsync(Request("echo broken input >&2; exit 4"), TestContext.Current.CancellationToken);

        var thrown = await act.Should().ThrowAsync<TranspilationFfmpegException>();
        thrown.Which.ExitCode.Should().Be(4);
        thrown.Which.Stderr.Should().Contain("broken input");
    }

    [Fact]
    public async Task RunAsync_outputMissing_throws()
    {
        RequireLinux();
        StatusIs(JobStatus.Processing);

        var act = () => RunAsync(Request("exit 0"), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<TranspilationFfmpegException>()).Which.ExitCode.Should().BeNull();
    }

    [Fact]
    public void MaxDuration_staysBelowTheRecoveryStallThreshold()
    {
        var stalled = TimeSpan.FromMinutes(new JobRecoveryOptions().TranspilationStalledMinutes);

        TranscodeProcess.MaxDuration.Should().BeLessThan(stalled);
    }

    [Fact]
    public void BrokerConsumerTimeout_staysAboveTheTranscodeCap()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "rabbitmq/definitions.template.json")))
            directory = directory.Parent;

        Assert.SkipWhen(directory is null, "The repository's broker definitions are not reachable from here.");

        using var definitions = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(directory.FullName, "rabbitmq/definitions.template.json")));

        var policy = definitions.RootElement.GetProperty("policies").EnumerateArray()
            .Single(p => p.GetProperty("pattern").GetString() == "^transpilation-queue$");
        var timeout = TimeSpan.FromMilliseconds(
            policy.GetProperty("definition").GetProperty("consumer-timeout").GetInt64());

        timeout.Should().BeGreaterThan(TranscodeProcess.MaxDuration);
    }

    [Fact]
    public async Task AudioTranspileAsync_healthyInput_producesManifest()
    {
        Assert.SkipUnless(OnPath("ffmpeg"), "ffmpeg is not installed.");
        StatusIs(JobStatus.Processing);
        var ct = TestContext.Current.CancellationToken;
        var directory = Directory.CreateTempSubdirectory("transcode-process-test-").FullName;

        try
        {
            var input = Path.Combine(directory, "source.flac");
            await GenerateAsync($"-f lavfi -i sine=duration=2 -c:a flac \"{input}\"", ct);

            var sut = new AudioTranspilationService(_unitOfWork, NullLogger<AudioTranspilationService>.Instance);

            var output = await sut.TranspileAsync(_jobId, input, Path.Combine(directory, "out"), [AudioRung.Kbps96], ct);

            File.Exists(Path.Combine(output.DashDirectory, "manifest.mpd")).Should().BeTrue();
            output.Lanes.Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task VideoTranspileAsync_healthyInput_producesManifest()
    {
        Assert.SkipUnless(OnPath("ffmpeg"), "ffmpeg is not installed.");
        StatusIs(JobStatus.Processing);
        var ct = TestContext.Current.CancellationToken;
        var directory = Directory.CreateTempSubdirectory("transcode-process-test-").FullName;

        try
        {
            var input = Path.Combine(directory, "source.mp4");
            await GenerateAsync(
                "-f lavfi -i testsrc2=size=160x90:rate=10:duration=2 -f lavfi -i sine=duration=2 " +
                $"-c:v libx264 -preset ultrafast -threads 1 -c:a aac \"{input}\"", ct);

            var sut = VideoService(VideoEncoderSelection.Cpu);

            var output = await sut.TranspileAsync(_jobId, input, Path.Combine(directory, "out"), [VideoRung.P360], ct);

            File.Exists(Path.Combine(output.DashDirectory, "manifest.mpd")).Should().BeTrue();
            output.Lanes.Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // /dev/null exists but is no GPU, so both GPU attempts fail inside ffmpeg and the CPU pass
    // has to finish the job from a clean output folder.
    [Fact]
    public async Task VideoTranspileAsync_gpuThatCannotEncode_fallsBackToTheCpu()
    {
        Assert.SkipUnless(OnPath("ffmpeg"), "ffmpeg is not installed.");
        StatusIs(JobStatus.Processing);
        var ct = TestContext.Current.CancellationToken;
        var directory = Directory.CreateTempSubdirectory("transcode-process-test-").FullName;

        try
        {
            var input = Path.Combine(directory, "source.mp4");
            await GenerateAsync(
                "-f lavfi -i testsrc2=size=640x360:rate=10:duration=2 -f lavfi -i sine=duration=2 " +
                $"-c:v libx264 -preset ultrafast -threads 1 -c:a aac \"{input}\"", ct);

            var sut = VideoService(new VideoEncoderSelection("/dev/null"));

            var output = await sut.TranspileAsync(_jobId, input, Path.Combine(directory, "out"),
                [VideoRung.P360], ct);

            var manifest = await File.ReadAllTextAsync(Path.Combine(output.DashDirectory, "manifest.mpd"), ct);
            manifest.Should().Contain("width=\"640\"").And.Contain("contentType=\"audio\"");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // Runs only on a machine whose first render node passes the VAAPI probe. A moving test pattern
    // is busy enough to need the capped re-encode on some GPUs and a flat colour never is; which
    // path a GPU takes varies, but the published lanes must fit their ceilings either way.
    [Theory]
    [InlineData("testsrc2=size=1280x720:rate=30:duration=8")]
    [InlineData("color=c=gray:size=1280x720:rate=30:duration=8")]
    public async Task VideoTranspileAsync_workingGpu_encodesEveryLaneOnItWithinItsCeiling(string source)
    {
        const string device = "/dev/dri/renderD128";
        Assert.SkipUnless(OnPath("ffmpeg") && OnPath("ffprobe"), "ffmpeg and ffprobe are not installed.");
        var ct = TestContext.Current.CancellationToken;
        var probe = await new VaapiEncoderProbe(NullLogger<VaapiEncoderProbe>.Instance).ProbeAsync(device, ct);
        Assert.SkipUnless(probe.Usable, $"No VAAPI GPU at {device}: {probe.Detail}");

        StatusIs(JobStatus.Processing);
        var directory = Directory.CreateTempSubdirectory("transcode-process-test-").FullName;
        var logger = new RecordingLogger<VideoTranspilationService>();

        try
        {
            var input = Path.Combine(directory, "source.mp4");
            await GenerateAsync(
                $"-f lavfi -i {source} -f lavfi -i sine=duration=8 " +
                $"-c:v libx264 -preset ultrafast -threads 1 -c:a aac \"{input}\"", ct);

            var selector = Substitute.For<IVideoEncoderSelector>();
            selector.SelectAsync(Arg.Any<CancellationToken>()).Returns(new VideoEncoderSelection(device));
            var sut = new VideoTranspilationService(logger, _unitOfWork, selector);

            var output = await sut.TranspileAsync(_jobId, input, Path.Combine(directory, "out"),
                [VideoRung.P360, VideoRung.P720], ct);

            foreach (var entry in logger.Entries)
                TestContext.Current.TestOutputHelper?.WriteLine($"{entry.Level}: {entry.Message}");

            var manifestPath = Path.Combine(output.DashDirectory, "manifest.mpd");
            var manifest = await File.ReadAllTextAsync(manifestPath, ct);
            manifest.Should().Contain("width=\"640\"").And.Contain("width=\"1280\"").And.Contain("bandwidth=\"2500000\"");
            manifest.Should().Contain("<S t=\"0\" d=\"92160\" />", "the forced keyframe cuts the first segment at 6 s");
            LaneBitrateCheck.FindProblem(manifestPath, [new(640, 360, 800, 28), new(1280, 720, 2500, 23)]).Should().BeNull();
            logger.Entries.Should().Contain(entry => entry.Message.Contains("with GpuDecodeAndEncode"));
            logger.Entries.Should().NotContain(entry => entry.Level == LogLevel.Warning);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task VideoTranspileAsync_sourceWithoutAudio_publishesVideoOnly()
    {
        Assert.SkipUnless(OnPath("ffmpeg") && OnPath("ffprobe"), "ffmpeg and ffprobe are not installed.");
        StatusIs(JobStatus.Processing);
        var ct = TestContext.Current.CancellationToken;
        var directory = Directory.CreateTempSubdirectory("transcode-process-test-").FullName;

        try
        {
            var input = Path.Combine(directory, "source.mp4");
            await GenerateAsync(
                $"-f lavfi -i testsrc2=size=160x90:rate=10:duration=2 -c:v libx264 -preset ultrafast -threads 1 \"{input}\"", ct);

            var sut = VideoService(VideoEncoderSelection.Cpu);

            var output = await sut.TranspileAsync(_jobId, input, Path.Combine(directory, "out"), [VideoRung.P360], ct);

            var manifest = await File.ReadAllTextAsync(Path.Combine(output.DashDirectory, "manifest.mpd"), ct);
            manifest.Should().Contain("contentType=\"video\"").And.NotContain("contentType=\"audio\"");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // Phones store portrait clips sideways with a rotation tag. The lanes must come out upright
    // at the clip's shape, and the 1080p rung, which would only upscale, gives way to a lane at
    // the clip's own size.
    [Fact]
    public async Task VideoTranspileAsync_rotatedPortraitSource_keepsItsShapeAndNeverUpscales()
    {
        Assert.SkipUnless(OnPath("ffmpeg") && OnPath("ffprobe"), "ffmpeg and ffprobe are not installed.");
        StatusIs(JobStatus.Processing);
        var ct = TestContext.Current.CancellationToken;
        var directory = Directory.CreateTempSubdirectory("transcode-process-test-").FullName;

        try
        {
            var stored = Path.Combine(directory, "stored.mp4");
            var input = Path.Combine(directory, "source.mp4");
            await GenerateAsync(
                $"-f lavfi -i testsrc2=size=1280x720:rate=10:duration=2 -c:v libx264 -preset ultrafast -threads 1 \"{stored}\"", ct);
            await GenerateAsync($"-display_rotation:v:0 90 -i \"{stored}\" -c copy \"{input}\"", ct);

            var sut = VideoService(VideoEncoderSelection.Cpu);

            var output = await sut.TranspileAsync(_jobId, input, Path.Combine(directory, "out"),
                [VideoRung.P360, VideoRung.P1080], ct);

            output.Lanes.Select(lane => (lane.Width, lane.Height, lane.BitrateKbps)).Should()
                .Equal((360, 640, 800), (720, 1280, 2500));

            var manifest = await File.ReadAllTextAsync(Path.Combine(output.DashDirectory, "manifest.mpd"), ct);
            manifest.Should().Contain("width=\"360\" height=\"640\"").And.Contain("width=\"720\" height=\"1280\"");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private VideoTranspilationService VideoService(VideoEncoderSelection encoder)
    {
        var selector = Substitute.For<IVideoEncoderSelector>();
        selector.SelectAsync(Arg.Any<CancellationToken>()).Returns(encoder);

        return new VideoTranspilationService(NullLogger<VideoTranspilationService>.Instance, _unitOfWork, selector);
    }

    private static async Task GenerateAsync(string arguments, CancellationToken ct)
    {
        var result = await Alexandria.Common.Processes.ExternalProcess.RunAsync(
            "ffmpeg", $"-v error {arguments}", NoLimit, NullLogger.Instance, ct);

        result.ExitCode.Should().Be(0, result.StdErrTail);
    }
}
