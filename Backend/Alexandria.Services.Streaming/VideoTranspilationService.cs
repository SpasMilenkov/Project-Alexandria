using System.Diagnostics;
using Alexandria.Common.Exceptions.Transpilation;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming;

public partial class VideoTranspilationService(
    ILogger<VideoTranspilationService> logger) : IVideoTranspilationService
{
    private sealed record VideoQualityLane(int Width, int Height, int BitrateKbps, int Crf);

    private static readonly IReadOnlyList<VideoQualityLane> ResolutionLadder =
    [
        new(640, 360, 800, 28),
        new(1280, 720, 2500, 23),
        new(1920, 1080, 5000, 21),
    ];

    /// <inheritdoc/>
    public async Task<TranspilationOutput> TranspileAsync(
        string inputPath,
        string outputDirectory,
        CancellationToken ct = default)
    {
        var dashDir = Path.Combine(outputDirectory, "dash");
        Directory.CreateDirectory(dashDir);

        LogTranspilationStarting(logger, inputPath, outputDirectory);

        await RunDashPassAsync(inputPath, dashDir, ct);

        LogTranspilationCompleted(logger, inputPath);

        return new TranspilationOutput
        {
            DashDirectory = dashDir,
            RootDirectory = outputDirectory,
            Lanes = ResolutionLadder
                .Select(l => new ProducedLane(StreamCodec.H264, l.BitrateKbps, l.Width, l.Height))
                .ToList()
        };
    }

    private async Task RunDashPassAsync(string inputPath, string dashDir, CancellationToken ct)
    {
        var manifestPath = Path.Combine(dashDir, "manifest.mpd");

        var args = new List<string> { "-i", $"\"{inputPath}\"" };

        for (var i = 0; i < ResolutionLadder.Count; i++)
        {
            var lane = ResolutionLadder[i];
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

        var argString = string.Join(" ", args);

        LogDashPassStarting(logger, inputPath);
        await RunFfmpegAsync(inputPath, argString, "video DASH", manifestPath, ct);
        LogDashPassCompleted(logger, manifestPath);
    }

    private static async Task RunFfmpegAsync(
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

        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
            throw new TranspilationFfmpegException(inputPath, operation, process.ExitCode, stderr);

        if (!File.Exists(expectedOutput))
            throw new TranspilationFfmpegException(inputPath, operation, expectedOutput);
    }
}