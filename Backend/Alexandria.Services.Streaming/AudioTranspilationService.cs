using System.Diagnostics;
using Alexandria.Common.Exceptions.Transpilation;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming;
using Microsoft.Extensions.Logging;
using TranspilationOutput = Alexandria.Dto.Files.Streaming.TranspilationOutput;

namespace Alexandria.Services.Streaming;

public partial class AudioTranspilationService(
    ILogger<AudioTranspilationService> logger) : IAudioTranspilationService
{
    private static readonly IReadOnlyList<int> BitrateLadder = [64, 128, 192];

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
            Lanes = BitrateLadder
                .Select(kbps => new ProducedLane(StreamCodec.Opus, kbps))
                .ToList()
        };
    }

    private async Task RunDashPassAsync(string inputPath, string dashDir, CancellationToken ct)
    {
        var manifestPath = Path.Combine(dashDir, "manifest.mpd");

        var args = new List<string> { "-i", $"\"{inputPath}\"", "-vn" };

        for (var i = 0; i < BitrateLadder.Count; i++)
        {
            args.AddRange(["-map", "0:a", $"-c:a:{i}", "libopus", $"-b:a:{i}", $"{BitrateLadder[i]}k"]);
        }

        args.AddRange([
            "-f", "dash",
            "-seg_duration", "6",
            "-adaptation_sets", "\"id=0,streams=a\"",
            $"\"{manifestPath}\""
        ]);

        var argString = string.Join(" ", args);

        LogDashPassStarting(logger, inputPath);
        await RunFfmpegAsync(inputPath, argString, "audio DASH", manifestPath, ct);
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