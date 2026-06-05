using System.Diagnostics;
using Alexandria.Common;
using Alexandria.Common.Exceptions.Transpilation;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming;
using Microsoft.Extensions.Logging;
using TranspilationOutput = Alexandria.Dto.Files.Streaming.TranspilationOutput;

namespace Alexandria.Services.Streaming;

public partial class AudioTranspilationService(
    IUnitOfWork unitOfWork,
    ILogger<AudioTranspilationService> logger) : IAudioTranspilationService
{
    private static readonly IReadOnlyDictionary<AudioRung, int> RungMap = new Dictionary<AudioRung, int>
    {
        [AudioRung.Kbps96] = 96,
        [AudioRung.Kbps128] = 128,
        [AudioRung.Kbps192] = 192,
        [AudioRung.Kbps256] = 256,
        [AudioRung.Kbps320] = 320
    };

    /// <inheritdoc/> 
    public async Task<TranspilationOutput> TranspileAsync(
        Guid jobId,
        string inputPath,
        string outputDirectory,
        AudioRung[] audioRungs,
        CancellationToken ct = default)
    {
        var lanes = audioRungs.OrderBy(r => r)
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
                .Select(kbps => new ProducedLane(StreamCodec.Opus, kbps))
                .ToList()
        };
    }

    private async Task RunDashPassAsync(Guid jobId, string inputPath, string dashDir, IReadOnlyList<int> lanes,
        CancellationToken ct)
    {
        var manifestPath = Path.Combine(dashDir, "manifest.mpd");

        var args = new List<string> { "-i", $"\"{inputPath}\"", "-vn" };

        for (var i = 0; i < lanes.Count; i++)
        {
            args.AddRange(["-map", "0:a", $"-c:a:{i}", "libopus", $"-b:a:{i}", $"{lanes[i]}k"]);
        }

        args.AddRange([
            "-f", "dash",
            "-seg_duration", "6",
            "-adaptation_sets", "\"id=0,streams=a\"",
            $"\"{manifestPath}\""
        ]);

        LogDashPassStarting(logger, inputPath);
        await RunFfmpegAsync(jobId, inputPath, string.Join(" ", args), "audio DASH", manifestPath, ct);
        LogDashPassCompleted(logger, manifestPath);
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
        try
        {
            await watchdog;
        }
        catch (OperationCanceledException)
        {
        }

        var stderr = await stderrTask;

        if (wasCancelled)
            throw new TranspilationCancelledException(jobId);

        if (process.ExitCode != 0)
            throw new TranspilationFfmpegException(inputPath, operation, process.ExitCode, stderr);

        if (!File.Exists(expectedOutput))
            throw new TranspilationFfmpegException(inputPath, operation, expectedOutput);
    }
}