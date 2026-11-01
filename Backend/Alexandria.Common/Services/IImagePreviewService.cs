namespace Alexandria.Common.Services;

public interface IImagePreviewService
{
    /// <summary>
    /// Generates the preview (fits 1280x720, never upscaled) and the padded 320x320 thumbnail
    /// from a single seekable source.
    /// </summary>
    Task<ImagePreviewArtifacts> GenerateAsync(Stream source, string? mimeType, CancellationToken ct = default);
}
