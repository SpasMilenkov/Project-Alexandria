using System.Diagnostics;
using System.Text.Json;
using DTO;
using DTO.Files;
using Microsoft.Extensions.Logging;
using Models.Enumerators;
using PreviewService.Media.Dto;

namespace PreviewService.Media;

public class MediaPreviewService(ILogger<MediaPreviewService> logger) : IMediaPreviewService
{
    private const int PreviewDurationSeconds = 30;
    private const int ThumbnailTimeSeconds = 5; // Extract thumbnail at 5 seconds

    /// <summary>
    /// Main entry point - routes to appropriate preview generation method based on file type
    /// </summary>
    public async Task<MediaPreviewResult?> GeneratePreviewAsync(
        string inputPath, 
        FileCategory fileCategory, 
        CancellationToken ct)
    {
        try
        {
            var metadata = await AnalyzeMediaAsync(inputPath, ct);
            
            return fileCategory switch
            {
                FileCategory.Video => await GenerateVideoPreviewAsync(inputPath, metadata, ct),
                FileCategory.Audio => await GenerateAudioPreviewAsync(inputPath, metadata, ct),
                _ => throw new NotSupportedException($"File category {fileCategory} not supported for media preview")
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate media preview for {InputPath}", inputPath);
            throw;
        }
    }

    /// <summary>
    /// Analyzes media file using ffprobe to get metadata
    /// </summary>
    private async Task<MediaMetadata> AnalyzeMediaAsync(string inputPath, CancellationToken ct)
    {
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
            throw new Exception("Failed to start ffprobe process");

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(ct);
            throw new Exception($"ffprobe failed with exit code {process.ExitCode}: {error}");
        }

        var probeData = JsonSerializer.Deserialize<FFprobeOutput>(output);
        if (probeData?.Format == null)
            throw new Exception("Failed to parse ffprobe output");

        var videoStream = probeData.Streams?.FirstOrDefault(s => s.CodecType == "video");
        var audioStream = probeData.Streams?.FirstOrDefault(s => s.CodecType == "audio");

        // Extract audio tags (artist, album, title, etc.)
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
            // Audio metadata
            Title = tags?.GetValueOrDefault("title"),
            Artist = tags?.GetValueOrDefault("artist"),
            Album = tags?.GetValueOrDefault("album"),
            Year = tags?.GetValueOrDefault("date"),
            Genre = tags?.GetValueOrDefault("genre")
        };
    }

    /// <summary>
    /// Generates thumbnail and preview clip for video files
    /// </summary>
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

    /// <summary>
    /// Generates thumbnail and preview clip for audio files
    /// Tries to extract embedded album artwork first, falls back to waveform if none exists
    /// </summary>
    private async Task<MediaPreviewResult> GenerateAudioPreviewAsync(
        string inputPath, 
        MediaMetadata metadata, 
        CancellationToken ct)
    {
        var outputDir = Path.GetTempPath();
        var baseName = Path.GetFileNameWithoutExtension(inputPath);
        var thumbnailPath = Path.Combine(outputDir, $"{baseName}_thumb.jpg");
        var previewPath = Path.Combine(outputDir, $"{baseName}_preview.mp3");

        var hasArtwork = await TryExtractAlbumArtworkAsync(inputPath, thumbnailPath, ct);
        
        if (!hasArtwork)
        {
            logger.LogInformation("No embedded artwork found, generating waveform for {InputPath}", inputPath);
            await GenerateWaveformAsync(inputPath, thumbnailPath, ct);
        }
        else
        {
            logger.LogInformation("Successfully extracted album artwork for {InputPath}", inputPath);
        }

        // Generate 30-second preview clip
        var previewDuration = Math.Min(PreviewDurationSeconds, metadata.Duration);
        await GenerateAudioClipAsync(inputPath, previewPath, previewDuration, ct);

        return new MediaPreviewResult
        {
            ThumbnailPath = thumbnailPath,
            PreviewPath = previewPath,
            Metadata = metadata
        };
    }

    /// <summary>
    /// Extracts a single frame as thumbnail from video
    /// </summary>
    private async Task GenerateThumbnailAsync(
        string inputPath, 
        string outputPath, 
        double timeSeconds, 
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-ss {timeSeconds} -i \"{inputPath}\" -vframes 1 -vf \"scale='min(1920,iw)':'min(1080,ih)':force_original_aspect_ratio=decrease\" -q:v 2 \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            throw new Exception("Failed to start ffmpeg process for thumbnail");

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(ct);
            logger.LogWarning("FFmpeg thumbnail generation warning: {Error}", error);
        }

        if (!File.Exists(outputPath))
            throw new Exception($"Thumbnail generation failed: {outputPath} not created");
    }

    /// <summary>
    /// Attempts to extract embedded album artwork from audio file
    /// Returns true if artwork was successfully extracted, false otherwise
    /// </summary>
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

            // Check if artwork was successfully extracted
            if (File.Exists(outputPath) && new FileInfo(outputPath).Length > 0)
            {
                logger.LogInformation("Successfully extracted album artwork from {InputPath}", inputPath);
                return true;
            }

            if (File.Exists(outputPath))
                File.Delete(outputPath);

            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to extract album artwork from {InputPath}", inputPath);
            
            // Clean up any partial file
            if (File.Exists(outputPath))
            {
                try { File.Delete(outputPath); } catch { /* ignore */ }
            }
            
            return false;
        }
    }

    /// <summary>
    /// Generates a waveform visualization for audio files
    /// </summary>
    private async Task GenerateWaveformAsync(
        string inputPath, 
        string outputPath, 
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-i \"{inputPath}\" -filter_complex \"[0:a]showwavespic=s=1920x1080:colors=0x3b82f6\" -frames:v 1 \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            throw new Exception("Failed to start ffmpeg process for waveform");

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(ct);
            logger.LogWarning("FFmpeg waveform generation warning: {Error}", error);
        }

        if (!File.Exists(outputPath))
            throw new Exception($"Waveform generation failed: {outputPath} not created");
    }

    /// <summary>
    /// Extracts first N seconds of video as preview clip
    /// </summary>
    private async Task GenerateVideoClipAsync(
        string inputPath, 
        string outputPath, 
        double durationSeconds, 
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-i \"{inputPath}\" -t {durationSeconds} -c:v libx264 -preset fast -crf 23 -c:a aac -b:a 128k -movflags +faststart \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            throw new Exception("Failed to start ffmpeg process for video clip");

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(ct);
            throw new Exception($"Video clip generation failed: {error}");
        }

        if (!File.Exists(outputPath))
            throw new Exception($"Video clip not created: {outputPath}");
    }
    
    /// <summary>
    /// Extracts first N seconds of audio as preview clip
    /// </summary>
    private async Task GenerateAudioClipAsync(
        string inputPath, 
        string outputPath, 
        double durationSeconds, 
        CancellationToken ct)
    {
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
            throw new Exception("Failed to start ffmpeg process for audio clip");

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(ct);
            throw new Exception($"Audio clip generation failed: {error}");
        }

        if (!File.Exists(outputPath))
            throw new Exception($"Audio clip not created: {outputPath}");
    }

    /// <summary>
    /// Optimizes video for progressive web streaming by moving moov atom to front
    /// This is already done in GenerateVideoClipAsync with -movflags +faststart,
    /// should be used for optimization of existing files
    /// </summary>
    private async Task OptimizeForStreamingAsync(string videoPath, CancellationToken ct)
    {
        // Check if already optimized 
        if (videoPath.Contains("_preview.mp4"))
        {
            // Already optimized during generation with faststart flag
            return;
        }

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
            throw new Exception("Failed to start ffmpeg process for optimization");

        await process.WaitForExitAsync(ct);

        if (process.ExitCode == 0 && File.Exists(tempPath))
        {
            File.Delete(videoPath);
            File.Move(tempPath, videoPath);
        }
    }
}