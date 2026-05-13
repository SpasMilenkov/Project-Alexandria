using System.Diagnostics;
using Alexandria.Common.Exceptions.Transpilation;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming;

public partial class AudioTranspilationService(
    ILogger<AudioTranspilationService> logger) : IAudioTranspilationService
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
            "-vn",
            "-c:a libopus -b:a 128k",
            "-f dash -seg_duration 6",
            $"\"{manifestPath}\"");

        LogDashPassStarting(logger, inputPath);
        await RunFfmpegAsync(inputPath, args, "audio DASH", manifestPath, ct);
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

        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        var stderr = await stderrTask;

        if (process.ExitCode != 0)
            throw new TranspilationFfmpegException(inputPath, operation, process.ExitCode, stderr);

        if (!File.Exists(expectedOutput))
            throw new TranspilationFfmpegException(inputPath, operation, expectedOutput);
    }
}