using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Preview.Media;

public partial class MediaPreviewService
{
    // GeneratePreviewAsync

    [LoggerMessage(4001, LogLevel.Debug,
        "Generating media preview for '{InputPath}' (category: {FileCategory})")]
    private static partial void LogGeneratingPreview(ILogger logger, string inputPath, string fileCategory);

    [LoggerMessage(4002, LogLevel.Debug,
        "Media preview generated successfully: thumbnail='{ThumbnailPath}' preview='{PreviewPath}'")]
    private static partial void LogPreviewGenerated(ILogger logger, string thumbnailPath, string previewPath);

    [LoggerMessage(4003, LogLevel.Error,
        "Failed to generate media preview for '{InputPath}'")]
    private static partial void LogPreviewFailed(ILogger logger, Exception ex, string inputPath);

    // ffprobe

    [LoggerMessage(4010, LogLevel.Debug,
        "Starting ffprobe analysis for '{InputPath}'")]
    private static partial void LogFfprobeStarting(ILogger logger, string inputPath);

    [LoggerMessage(4011, LogLevel.Debug,
        "ffprobe analysis completed for '{InputPath}'")]
    private static partial void LogFfprobeCompleted(ILogger logger, string inputPath);

    [LoggerMessage(4012, LogLevel.Error,
        "ffprobe process could not be started for '{InputPath}'")]
    private static partial void LogFfprobeStartFailed(ILogger logger, string inputPath);

    [LoggerMessage(4013, LogLevel.Error,
        "ffprobe exited with code {ExitCode} for '{InputPath}': {Error}")]
    private static partial void LogFfprobeFailed(ILogger logger, int exitCode, string inputPath, string error);

    [LoggerMessage(4014, LogLevel.Error,
        "ffprobe output could not be parsed for '{InputPath}'")]
    private static partial void LogFfprobeParseFailed(ILogger logger, string inputPath);

    // Thumbnail

    [LoggerMessage(4020, LogLevel.Debug,
        "Generating thumbnail for '{InputPath}' at {TimeSeconds}s")]
    private static partial void LogThumbnailStarting(ILogger logger, string inputPath, double timeSeconds);

    [LoggerMessage(4021, LogLevel.Debug,
        "Thumbnail generated: '{OutputPath}'")]
    private static partial void LogThumbnailGenerated(ILogger logger, string outputPath);

    [LoggerMessage(4022, LogLevel.Error,
        "ffmpeg thumbnail process could not be started for '{InputPath}'")]
    private static partial void LogThumbnailStartFailed(ILogger logger, string inputPath);

    [LoggerMessage(4023, LogLevel.Warning,
        "ffmpeg thumbnail non-fatal warning for '{InputPath}': {Error}")]
    private static partial void LogThumbnailWarning(ILogger logger, string inputPath, string error);

    // Album artwork

    [LoggerMessage(4030, LogLevel.Debug,
        "Attempting to extract embedded album artwork from '{InputPath}'")]
    private static partial void LogArtworkExtracting(ILogger logger, string inputPath);

    [LoggerMessage(4031, LogLevel.Information,
        "Successfully extracted album artwork from '{InputPath}'")]
    private static partial void LogArtworkExtracted(ILogger logger, string inputPath);

    [LoggerMessage(4032, LogLevel.Information,
        "No embedded artwork found for '{InputPath}', generating waveform instead")]
    private static partial void LogArtworkNotFound(ILogger logger, string inputPath);

    [LoggerMessage(4033, LogLevel.Warning,
        "Failed to extract album artwork from '{InputPath}', falling back to waveform")]
    private static partial void LogArtworkExtractionFailed(ILogger logger, Exception ex, string inputPath);

    // Waveform

    [LoggerMessage(4040, LogLevel.Debug,
        "Generating waveform for '{InputPath}'")]
    private static partial void LogWaveformStarting(ILogger logger, string inputPath);

    [LoggerMessage(4041, LogLevel.Debug,
        "Waveform generated: '{OutputPath}'")]
    private static partial void LogWaveformGenerated(ILogger logger, string outputPath);

    [LoggerMessage(4042, LogLevel.Error,
        "ffmpeg waveform process could not be started for '{InputPath}'")]
    private static partial void LogWaveformStartFailed(ILogger logger, string inputPath);

    [LoggerMessage(4043, LogLevel.Warning,
        "ffmpeg waveform non-fatal warning for '{InputPath}': {Error}")]
    private static partial void LogWaveformWarning(ILogger logger, string inputPath, string error);

    // Video clip

    [LoggerMessage(4050, LogLevel.Debug,
        "Generating video clip for '{InputPath}' (duration: {DurationSeconds}s)")]
    private static partial void LogVideoClipStarting(ILogger logger, string inputPath, double durationSeconds);

    [LoggerMessage(4051, LogLevel.Debug,
        "Video clip generated: '{OutputPath}'")]
    private static partial void LogVideoClipGenerated(ILogger logger, string outputPath);

    [LoggerMessage(4052, LogLevel.Error,
        "ffmpeg video clip process could not be started for '{InputPath}'")]
    private static partial void LogVideoClipStartFailed(ILogger logger, string inputPath);

    [LoggerMessage(4053, LogLevel.Error,
        "ffmpeg video clip failed for '{InputPath}': {Error}")]
    private static partial void LogVideoClipFailed(ILogger logger, string inputPath, string error);

    // Audio clip

    [LoggerMessage(4060, LogLevel.Debug,
        "Generating audio clip for '{InputPath}' (duration: {DurationSeconds}s)")]
    private static partial void LogAudioClipStarting(ILogger logger, string inputPath, double durationSeconds);

    [LoggerMessage(4061, LogLevel.Debug,
        "Audio clip generated: '{OutputPath}'")]
    private static partial void LogAudioClipGenerated(ILogger logger, string outputPath);

    [LoggerMessage(4062, LogLevel.Error,
        "ffmpeg audio clip process could not be started for '{InputPath}'")]
    private static partial void LogAudioClipStartFailed(ILogger logger, string inputPath);

    [LoggerMessage(4063, LogLevel.Error,
        "ffmpeg audio clip failed for '{InputPath}': {Error}")]
    private static partial void LogAudioClipFailed(ILogger logger, string inputPath, string error);

    // Streaming optimization

    [LoggerMessage(4070, LogLevel.Debug,
        "Optimizing '{VideoPath}' for streaming")]
    private static partial void LogOptimizationStarting(ILogger logger, string videoPath);

    [LoggerMessage(4071, LogLevel.Debug,
        "'{VideoPath}' already contains faststart flag, skipping optimization")]
    private static partial void LogOptimizationSkipped(ILogger logger, string videoPath);

    [LoggerMessage(4072, LogLevel.Debug,
        "Streaming optimization completed for '{VideoPath}'")]
    private static partial void LogOptimizationCompleted(ILogger logger, string videoPath);

    [LoggerMessage(4073, LogLevel.Error,
        "ffmpeg optimization process could not be started for '{VideoPath}'")]
    private static partial void LogOptimizationStartFailed(ILogger logger, string videoPath);
}