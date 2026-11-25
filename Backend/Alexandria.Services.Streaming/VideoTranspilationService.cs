using Alexandria.Common;
using Alexandria.Common.Exceptions.Transpilation;
using Alexandria.Common.Processes;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming;
using Alexandria.Services.Streaming.VideoEncoding;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming;

public partial class VideoTranspilationService(
    ILogger<VideoTranspilationService> logger,
    IUnitOfWork unitOfWork,
    IVideoEncoderSelector encoderSelector) : IVideoTranspilationService
{
    // Reading stream headers is quick; a source that takes longer is not worth transcoding.
    private static readonly TimeSpan AudioProbeTimeout = TimeSpan.FromMinutes(1);

    private static readonly IReadOnlyDictionary<VideoRung, VideoQualityLane> RungMap =
        new Dictionary<VideoRung, VideoQualityLane>
        {
            [VideoRung.P360] = new(640, 360, 800, 28),
            [VideoRung.P480] = new(854, 480, 1500, 26),
            [VideoRung.P720] = new(1280, 720, 2500, 23),
            [VideoRung.P1080] = new(1920, 1080, 5000, 21),
            [VideoRung.P1440] = new(2560, 1440, 8000, 19),
            [VideoRung.P2160] = new(3840, 2160, 16000, 17),
        };

    /// <inheritdoc/>
    public async Task<TranspilationOutput> TranspileAsync(
        Guid jobId,
        string inputPath,
        string outputDirectory,
        VideoRung[] videoRungs,
        CancellationToken ct = default)
    {
        var lanes = videoRungs
            .OrderBy(r => r)
            .Select(r => RungMap[r])
            .ToList();

        var dashDir = Path.Combine(outputDirectory, "dash");
        Directory.CreateDirectory(dashDir);

        LogTranspilationStarting(logger, inputPath, outputDirectory);

        await RunDashPassAsync(jobId, inputPath, dashDir, lanes, ct);

        LogTranspilationCompleted(logger, inputPath);

        return new TranspilationOutput
        {
            DashDirectory = dashDir,
            RootDirectory = outputDirectory,
            Lanes = lanes
                .Select(l => new ProducedLane(StreamCodec.H264, l.BitrateKbps, l.Width, l.Height))
                .ToList()
        };
    }


    private async Task RunDashPassAsync(
        Guid jobId,
        string inputPath,
        string dashDir,
        IReadOnlyList<VideoQualityLane> lanes,
        CancellationToken ct)
    {
        var manifestPath = Path.Combine(dashDir, "manifest.mpd");
        var hasAudio = await HasAudioAsync(inputPath, ct);
        var encoder = await encoderSelector.SelectAsync(ct);

        await VideoEncodeFallback.RunAsync(
            VideoEncodeFallback.AttemptsFor(encoder),
            attempt => RunAttemptAsync(attempt),
            (failed, next, ex) =>
            {
                LogAttemptFailed(logger, inputPath, failed, next, ex.ExitCode, LastLine(ex.Stderr));
                ClearOutput(dashDir);
            });

        LogDashPassCompleted(logger, manifestPath);

        async Task RunAttemptAsync(VideoEncodeAttempt attempt)
        {
            if (attempt == VideoEncodeAttempt.Cpu)
            {
                await RunPassAsync(attempt, DashVideoArguments.ForCpu(inputPath, manifestPath, lanes, hasAudio));
                return;
            }

            var device = encoder.GpuDevice ?? throw new InvalidOperationException("A GPU attempt needs a selected GPU.");
            var gpuDecode = attempt == VideoEncodeAttempt.GpuDecodeAndEncode;

            // Constant QP keeps static footage small; busy footage that runs past a lane's
            // ceiling is encoded again capped, since it needs the whole budget anyway.
            await RunPassAsync(attempt, DashVideoArguments.ForGpu(inputPath, manifestPath, lanes, hasAudio, device,
                gpuDecode, GpuRateControl.ConstantQuality));

            if (LaneBitrateCheck.FindProblem(manifestPath, lanes) is not { } problem)
                return;

            LogCappedReencode(logger, inputPath, problem);
            ClearOutput(dashDir);

            await RunPassAsync(attempt, DashVideoArguments.ForGpu(inputPath, manifestPath, lanes, hasAudio, device,
                gpuDecode, GpuRateControl.CappedBitrate));
        }

        async Task RunPassAsync(VideoEncodeAttempt attempt, string arguments)
        {
            LogDashPassStarting(logger, inputPath, attempt);

            await TranscodeProcess.RunFfmpegAsync(
                new TranscodeRequest(jobId, inputPath, arguments, $"video DASH ({attempt})", manifestPath),
                unitOfWork,
                logger,
                ct);
        }
    }

    // A pass that is replaced leaves segments the next one must not mix with its own.
    private static void ClearOutput(string dashDir)
    {
        Directory.Delete(dashDir, recursive: true);
        Directory.CreateDirectory(dashDir);
    }

    private static string LastLine(string? stderr) =>
        stderr?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault()
        ?? "no output";

    private async Task<bool> HasAudioAsync(string inputPath, CancellationToken ct)
    {
        var result = await ExternalProcess.RunAsync(
            "ffprobe", DashVideoArguments.AudioProbe(inputPath), AudioProbeTimeout, logger, ct);

        if (result.ExitCode != 0)
            throw new TranspilationFfprobeException(inputPath, result.ExitCode, result.StdErrTail);

        var hasAudio = !string.IsNullOrWhiteSpace(result.StdOut);

        if (!hasAudio)
            LogSourceWithoutAudio(logger, inputPath);

        return hasAudio;
    }
}
