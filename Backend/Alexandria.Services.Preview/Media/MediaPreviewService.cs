using System.Diagnostics;
using System.Text.Json;
using Alexandria.Common.Exceptions.Preview.Media;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Services.Preview.Media.Dto;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Preview.Media;

public partial class MediaPreviewService(ILogger<MediaPreviewService> logger) : IMediaPreviewService
{
    private const int PreviewDurationSeconds = 30;
    private const int ThumbnailTimeSeconds = 5;

    /// <inheritdoc/>
    public async Task<MediaPreviewResult?> GeneratePreviewAsync(
        string inputPath,
        FileCategory fileCategory,
        CancellationToken ct)
    {
        LogGeneratingPreview(logger, inputPath, fileCategory.ToString());
        try
        {
            var metadata = await AnalyzeMediaAsync(inputPath, ct);

            var result = fileCategory switch
            {
                FileCategory.Video => await GenerateVideoPreviewAsync(inputPath, metadata, ct),
                FileCategory.Audio => await GenerateAudioPreviewAsync(inputPath, metadata, ct),
                _ => throw new UnsupportedMediaCategoryException(fileCategory)
            };

            LogPreviewGenerated(logger, result.ThumbnailPath, result.PreviewPath);
            return result;
        }
        catch (Exception ex)
        {
            LogPreviewFailed(logger, ex, inputPath);
            throw;
        }
    }

    private async Task<MediaMetadata> AnalyzeMediaAsync(string inputPath, CancellationToken ct)
    {
        LogFfprobeStarting(logger, inputPath);

        var psi = new ProcessStartInfo
        {
            FileName = "ffprobe",
            Arguments = $"-v quiet -print_format json -show_format -show_streams \"{inputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            LogFfprobeStartFailed(logger, inputPath);
            throw new FfprobeException(inputPath);
        }

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(ct);
            LogFfprobeFailed(logger, process.ExitCode, inputPath, error);
            throw new FfprobeException(inputPath, process.ExitCode, error);
        }

        var probeData = JsonSerializer.Deserialize<FFprobeOutput>(output);
        if (probeData?.Format == null)
        {
            LogFfprobeParseFailed(logger, inputPath);
            throw new FfprobeException(inputPath, $"Failed to parse ffprobe output for '{inputPath}'.");
        }

        LogFfprobeCompleted(logger, inputPath);

        var videoStream = probeData.Streams?.FirstOrDefault(s => s.CodecType == "video");
        var audioStream = probeData.Streams?.FirstOrDefault(s => s.CodecType == "audio");
        var tags = probeData.Format?.Tags;

        return new MediaMetadata
        {
            Duration = double.TryParse(probeData.Format?.Duration, out var dur) ? dur : 0,
            FileSize = long.TryParse(probeData.Format?.Size, out var size) ? size : 0,
            BitrateMbps = long.TryParse(probeData.Format?.BitRate, out var br) ? br / 1_000_000.0 : 0,
            FormatName = probeData.Format?.FormatName,
            VideoCodec = videoStream?.CodecName,
            AudioCodec = audioStream?.CodecName,
            Width = videoStream?.Width ?? 0,
            Height = videoStream?.Height ?? 0,
            HasVideo = videoStream != null,
            HasAudio = audioStream != null,
            HasEmbeddedArtwork = probeData.Streams?.Any(s =>
                s.CodecType == "video" &&
                (s.Disposition?.AttachedPic ?? 0) == 1) ?? false,
            Title = tags?.GetValueOrDefault("title"),
            Artist = tags?.GetValueOrDefault("artist"),
            Album = tags?.GetValueOrDefault("album"),
            Year = tags?.GetValueOrDefault("date"),
            Genre = tags?.GetValueOrDefault("genre")
        };
    }

    private async Task<MediaPreviewResult> GenerateVideoPreviewAsync(
        string inputPath,
        MediaMetadata metadata,
        CancellationToken ct)
    {
        var outputDir = Path.GetTempPath();
        var baseName = Path.GetFileNameWithoutExtension(inputPath);
        var thumbnailPath = Path.Combine(outputDir, $"{baseName}_thumb.jpg");
        var previewPath = Path.Combine(outputDir, $"{baseName}_preview.mp4");

        var thumbnailTime = Math.Min(ThumbnailTimeSeconds, metadata.Duration * 0.1);
        await GenerateThumbnailAsync(inputPath, thumbnailPath, thumbnailTime, ct);

        var previewDuration = Math.Min(PreviewDurationSeconds, metadata.Duration);
        await GenerateVideoClipAsync(inputPath, previewPath, previewDuration, ct);

        await OptimizeForStreamingAsync(previewPath, ct);

        return new MediaPreviewResult
        {
            ThumbnailPath = thumbnailPath,
            PreviewPath = previewPath,
            Metadata = metadata
        };
    }

    private async Task<MediaPreviewResult> GenerateAudioPreviewAsync(
        string inputPath,
        MediaMetadata metadata,
        CancellationToken ct)
    {
        var outputDir = Path.GetTempPath();
        var baseName = Path.GetFileNameWithoutExtension(inputPath);
        var thumbnailPath = Path.Combine(outputDir, $"{baseName}_thumb.jpg");
        var previewPath = Path.Combine(outputDir, $"{baseName}_preview.mp3");

        LogArtworkExtracting(logger, inputPath);
        var hasArtwork = await TryExtractAlbumArtworkAsync(inputPath, thumbnailPath, ct);

        if (!hasArtwork)
        {
            LogArtworkNotFound(logger, inputPath);
            await GenerateWaveformAsync(inputPath, thumbnailPath, ct);
        }
        else
        {
            LogArtworkExtracted(logger, inputPath);
        }

        var previewDuration = Math.Min(PreviewDurationSeconds, metadata.Duration);
        await GenerateAudioClipAsync(inputPath, previewPath, previewDuration, ct);

        return new MediaPreviewResult
        {
            ThumbnailPath = thumbnailPath,
            PreviewPath = previewPath,
            Metadata = metadata
        };
    }

    private async Task GenerateThumbnailAsync(
        string inputPath,
        string outputPath,
        double timeSeconds,
        CancellationToken ct)
    {
        LogThumbnailStarting(logger, inputPath, timeSeconds);

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-ss {timeSeconds} -i \"{inputPath}\" -vframes 1 " +
                        $"-vf \"scale='min(1920,iw)':'min(1080,ih)':force_original_aspect_ratio=decrease\" " +
                        $"-q:v 2 \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            LogThumbnailStartFailed(logger, inputPath);
            throw new FfmpegException(inputPath, "thumbnail");
        }

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(ct);
            LogThumbnailWarning(logger, inputPath, error);
        }

        if (!File.Exists(outputPath))
            throw new FfmpegException(inputPath, "thumbnail", outputPath);

        LogThumbnailGenerated(logger, outputPath);
    }

    private async Task<bool> TryExtractAlbumArtworkAsync(
        string inputPath,
        string outputPath,
        CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-i \"{inputPath}\" -an -vcodec mjpeg \"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return false;

            await process.WaitForExitAsync(ct);

            if (File.Exists(outputPath) && new FileInfo(outputPath).Length > 0)
                return true;

            if (File.Exists(outputPath))
                File.Delete(outputPath);

            return false;
        }
        catch (Exception ex)
        {
            LogArtworkExtractionFailed(logger, ex, inputPath);

            if (File.Exists(outputPath))
            {
                try
                {
                    File.Delete(outputPath);
                }
                catch
                {
                    /* best effort */
                }
            }

            return false;
        }
    }

    private async Task GenerateWaveformAsync(
        string inputPath,
        string outputPath,
        CancellationToken ct)
    {
        LogWaveformStarting(logger, inputPath);

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-i \"{inputPath}\" " +
                        $"-filter_complex \"[0:a]showwavespic=s=1920x1080:colors=0x3b82f6\" " +
                        $"-frames:v 1 \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            LogWaveformStartFailed(logger, inputPath);
            throw new FfmpegException(inputPath, "waveform");
        }

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(ct);
            LogWaveformWarning(logger, inputPath, error);
        }

        if (!File.Exists(outputPath))
            throw new FfmpegException(inputPath, "waveform", outputPath);

        LogWaveformGenerated(logger, outputPath);
    }

    private async Task GenerateVideoClipAsync(
        string inputPath,
        string outputPath,
        double durationSeconds,
        CancellationToken ct)
    {
        LogVideoClipStarting(logger, inputPath, durationSeconds);

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-i \"{inputPath}\" -t {durationSeconds} " +
                        $"-c:v libx264 -preset fast -crf 23 -c:a aac -b:a 128k " +
                        $"-movflags +faststart \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            LogVideoClipStartFailed(logger, inputPath);
            throw new FfmpegException(inputPath, "video clip");
        }

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(ct);
            LogVideoClipFailed(logger, inputPath, error);
            throw new FfmpegException(inputPath, "video clip", process.ExitCode, error);
        }

        if (!File.Exists(outputPath))
            throw new FfmpegException(inputPath, "video clip", outputPath);

        LogVideoClipGenerated(logger, outputPath);
    }

    private async Task GenerateAudioClipAsync(
        string inputPath,
        string outputPath,
        double durationSeconds,
        CancellationToken ct)
    {
        LogAudioClipStarting(logger, inputPath, durationSeconds);

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-i \"{inputPath}\" -t {durationSeconds} -vn -c:a libmp3lame -b:a 128k \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            LogAudioClipStartFailed(logger, inputPath);
            throw new FfmpegException(inputPath, "audio clip");
        }

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(ct);
            LogAudioClipFailed(logger, inputPath, error);
            throw new FfmpegException(inputPath, "audio clip", process.ExitCode, error);
        }

        if (!File.Exists(outputPath))
            throw new FfmpegException(inputPath, "audio clip", outputPath);

        LogAudioClipGenerated(logger, outputPath);
    }

    private async Task OptimizeForStreamingAsync(string videoPath, CancellationToken ct)
    {
        if (videoPath.Contains("_preview.mp4"))
        {
            LogOptimizationSkipped(logger, videoPath);
            return;
        }

        LogOptimizationStarting(logger, videoPath);

        var tempPath = $"{videoPath}.temp.mp4";

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-i \"{videoPath}\" -c copy -movflags +faststart \"{tempPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            LogOptimizationStartFailed(logger, videoPath);
            throw new FfmpegException(videoPath, "streaming optimization");
        }

        await process.WaitForExitAsync(ct);

        if (process.ExitCode == 0 && File.Exists(tempPath))
        {
            File.Delete(videoPath);
            File.Move(tempPath, videoPath);
            LogOptimizationCompleted(logger, videoPath);
        }
    }
}