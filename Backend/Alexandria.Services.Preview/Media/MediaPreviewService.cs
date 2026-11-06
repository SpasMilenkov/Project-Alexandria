using System.Text.Json;
using Alexandria.Common.Exceptions.Preview.Media;
using Alexandria.Common.Processes;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Services.Preview.Media.Dto;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Preview.Media;

public partial class MediaPreviewService(ILogger<MediaPreviewService> logger) : IMediaPreviewService
{
    private const int PreviewDurationSeconds = 30;
    private const int ThumbnailTimeSeconds = 5;

    // The steps of one preview must total less than the job recovery stall threshold, because
    // preview jobs send no heartbeat and would otherwise be failed while still running.
    private static readonly TimeSpan FfprobeTimeout = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ThumbnailTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ArtworkTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan WaveformTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan VideoClipTimeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan AudioClipTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan OptimizationTimeout = TimeSpan.FromMinutes(2);

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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogPreviewFailed(logger, ex, inputPath);
            throw;
        }
    }

    private async Task<MediaMetadataDto> AnalyzeMediaAsync(string inputPath, CancellationToken ct)
    {
        LogFfprobeStarting(logger, inputPath);

        var result = await ExternalProcess.RunAsync(
            "ffprobe",
            $"-v quiet -print_format json -show_format -show_streams \"{inputPath}\"",
            FfprobeTimeout,
            logger,
            ct);

        if (result.ExitCode != 0)
        {
            LogFfprobeFailed(logger, result.ExitCode, inputPath, result.StdErrTail);
            throw new FfprobeException(inputPath, result.ExitCode, result.StdErrTail);
        }

        var probeData = JsonSerializer.Deserialize<FFprobeOutput>(result.StdOut);
        if (probeData?.Format == null)
        {
            LogFfprobeParseFailed(logger, inputPath);
            throw new FfprobeException(inputPath, $"Failed to parse ffprobe output for '{inputPath}'.");
        }

        LogFfprobeCompleted(logger, inputPath);

        return MapMetadata(probeData);
    }

    internal static MediaMetadataDto MapMetadata(FFprobeOutput probeData)
    {
        var videoStream = probeData.Streams?.FirstOrDefault(s => s.CodecType == "video");
        var audioStream = probeData.Streams?.FirstOrDefault(s => s.CodecType == "audio");
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (probeData.Format?.Tags is { } formatTags)
        {
            foreach (var (key, value) in formatTags)
                tags[key] = value;
        }

        return new MediaMetadataDto
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
            Title = tags.GetValueOrDefault("title"),
            Artist = tags.GetValueOrDefault("artist"),
            Album = tags.GetValueOrDefault("album"),
            Year = tags.GetValueOrDefault("date"),
            Genre = tags.GetValueOrDefault("genre")
        };
    }

    private async Task<MediaPreviewResult> GenerateVideoPreviewAsync(
        string inputPath,
        MediaMetadataDto metadata,
        CancellationToken ct)
    {
        var outputDir = Path.GetDirectoryName(inputPath) ?? Path.GetTempPath();
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
        MediaMetadataDto metadata,
        CancellationToken ct)
    {
        var outputDir = Path.GetDirectoryName(inputPath) ?? Path.GetTempPath();
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

        var result = await ExternalProcess.RunAsync(
            "ffmpeg",
            $"-ss {timeSeconds} -i \"{inputPath}\" -vframes 1 " +
            $"-vf \"scale='min(1920,iw)':'min(1080,ih)':force_original_aspect_ratio=decrease\" " +
            $"-q:v 2 \"{outputPath}\"",
            ThumbnailTimeout,
            logger,
            ct);

        if (result.ExitCode != 0)
            LogThumbnailWarning(logger, inputPath, result.StdErrTail);

        if (!File.Exists(outputPath))
            throw new FfmpegException(inputPath, "thumbnail", outputPath);

        LogThumbnailGenerated(logger, outputPath);
    }

    internal async Task<bool> TryExtractAlbumArtworkAsync(
        string inputPath,
        string outputPath,
        CancellationToken ct)
    {
        try
        {
            await ExternalProcess.RunAsync(
                "ffmpeg",
                $"-i \"{inputPath}\" -an -vcodec mjpeg \"{outputPath}\"",
                ArtworkTimeout,
                logger,
                ct);

            if (File.Exists(outputPath) && new FileInfo(outputPath).Length > 0)
                return true;

            if (File.Exists(outputPath))
                File.Delete(outputPath);

            return false;
        }
        catch (OperationCanceledException)
        {
            // Not a missing artwork: the waveform fallback must not run after a cancellation.
            TryDeletePartialArtwork(outputPath);
            throw;
        }
        catch (Exception ex)
        {
            LogArtworkExtractionFailed(logger, ex, inputPath);
            TryDeletePartialArtwork(outputPath);
            return false;
        }
    }

    private void TryDeletePartialArtwork(string outputPath)
    {
        try
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogArtworkCleanupFailed(logger, ex, outputPath);
        }
    }

    private async Task GenerateWaveformAsync(
        string inputPath,
        string outputPath,
        CancellationToken ct)
    {
        LogWaveformStarting(logger, inputPath);

        var result = await ExternalProcess.RunAsync(
            "ffmpeg",
            $"-i \"{inputPath}\" " +
            $"-filter_complex \"[0:a]showwavespic=s=1920x1080:colors=0x3b82f6\" " +
            $"-frames:v 1 \"{outputPath}\"",
            WaveformTimeout,
            logger,
            ct);

        if (result.ExitCode != 0)
            LogWaveformWarning(logger, inputPath, result.StdErrTail);

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

        var result = await ExternalProcess.RunAsync(
            "ffmpeg",
            $"-i \"{inputPath}\" -t {durationSeconds} " +
            $"-c:v libx264 -preset fast -crf 23 -c:a aac -b:a 128k " +
            $"-movflags +faststart \"{outputPath}\"",
            VideoClipTimeout,
            logger,
            ct);

        if (result.ExitCode != 0)
        {
            LogVideoClipFailed(logger, inputPath, result.StdErrTail);
            throw new FfmpegException(inputPath, "video clip", result.ExitCode, result.StdErrTail);
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

        var result = await ExternalProcess.RunAsync(
            "ffmpeg",
            $"-i \"{inputPath}\" -t {durationSeconds} -vn -c:a libmp3lame -b:a 128k \"{outputPath}\"",
            AudioClipTimeout,
            logger,
            ct);

        if (result.ExitCode != 0)
        {
            LogAudioClipFailed(logger, inputPath, result.StdErrTail);
            throw new FfmpegException(inputPath, "audio clip", result.ExitCode, result.StdErrTail);
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

        var result = await ExternalProcess.RunAsync(
            "ffmpeg",
            $"-i \"{videoPath}\" -c copy -movflags +faststart \"{tempPath}\"",
            OptimizationTimeout,
            logger,
            ct);

        if (result.ExitCode == 0 && File.Exists(tempPath))
        {
            File.Delete(videoPath);
            File.Move(tempPath, videoPath);
            LogOptimizationCompleted(logger, videoPath);
        }
    }
}
