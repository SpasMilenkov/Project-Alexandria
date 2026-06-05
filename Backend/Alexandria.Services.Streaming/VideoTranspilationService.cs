using System.Diagnostics;
using Alexandria.Common;
using Alexandria.Common.Exceptions.Transpilation;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming;

public partial class VideoTranspilationService(
    ILogger<VideoTranspilationService> logger,
    IUnitOfWork unitOfWork) : IVideoTranspilationService
{
    private sealed record VideoQualityLane(int Width, int Height, int BitrateKbps, int Crf);

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

        var args = new List<string> { "-i", $"\"{inputPath}\"" };

        for (var i = 0; i < lanes.Count; i++)
        {
            var lane = lanes[i];
            args.AddRange([
                "-map", "0:v",
                $"-c:v:{i}", "libx264",
                "-preset", "fast",
                $"-crf", lane.Crf.ToString(),
                $"-vf:{i}", $"scale={lane.Width}:{lane.Height}",
                $"-b:v:{i}", $"{lane.BitrateKbps}k",
            ]);
        }

        args.AddRange([
            "-map", "0:a",
            "-c:a", "aac",
            "-b:a", "128k",
            "-f", "dash",
            "-seg_duration", "6",
            "-adaptation_sets", "\"id=0,streams=v id=1,streams=a\"",
            $"\"{manifestPath}\""
        ]);

        await RunFfmpegAsync(jobId, inputPath, string.Join(" ", args), "video DASH", manifestPath, ct);
    }

    private async Task RunFfmpegAsync(
        Guid jobId,
        string inputPath,
        string arguments,
        string operation,
        string expectedOutput,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
                            ?? throw new TranspilationFfmpegException(inputPath, operation);

        using var killCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var wasCancelled = false;

        var watchdog = Task.Run(async () =>
        {
            while (!killCts.Token.IsCancellationRequested)
            {
                await Task.Delay(10_000, killCts.Token).ConfigureAwait(false);
                var status = await unitOfWork.TranspilationJobs.GetTranspilationStatusAsync(jobId, killCts.Token);
                if (status == TranspilationStatus.CancellationRequested)
                {
                    wasCancelled = true;
                    process.Kill(entireProcessTree: true);
                    break;
                }
            }
        }, killCts.Token);

        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        await killCts.CancelAsync();
        var stderr = await stderrTask;

        if (wasCancelled)
            throw new TranspilationCancelledException(jobId);

        if (process.ExitCode != 0)
            throw new TranspilationFfmpegException(inputPath, operation, process.ExitCode, stderr);

        if (!File.Exists(expectedOutput))
            throw new TranspilationFfmpegException(inputPath, operation, expectedOutput);
    }
}