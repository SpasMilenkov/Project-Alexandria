using System.Diagnostics;
using Alexandria.Common.Exceptions.Transpilation;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming;

public partial class VideoTranspilationService(
    ILogger<VideoTranspilationService> logger) : IVideoTranspilationService
{
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
            RootDirectory = outputDirectory
        };
    }

    private async Task RunDashPassAsync(string inputPath, string dashDir, CancellationToken ct)
    {
        var manifestPath = Path.Combine(dashDir, "manifest.mpd");

        var args = string.Join(" ",
            $"-i \"{inputPath}\"",
            "-c:v libx264 -preset fast -crf 23",
            "-c:a aac -b:a 128k",
            "-f dash -seg_duration 6",
            "-init_seg_name init.mp4",
            "-media_seg_name \"seg$Number%03d$.m4s\"",
            $"\"{manifestPath}\"");

        LogDashPassStarting(logger, inputPath);

        await RunFfmpegAsync(inputPath, args, "video DASH", manifestPath, ct);

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

        using var process = Process.Start(psi);

        if (process is null)
            throw new TranspilationFfmpegException(inputPath, operation);

        // Drain stderr concurrently to avoid blocking the process on a full pipe buffer
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        var stderr = await stderrTask;

        if (process.ExitCode != 0)
            throw new TranspilationFfmpegException(inputPath, operation, process.ExitCode, stderr);

        if (!File.Exists(expectedOutput))
            throw new TranspilationFfmpegException(inputPath, operation, expectedOutput);
    }
}