using DTO;

namespace PreviewService.Media.Dto;

/// <summary>
/// Result of media preview generation containing paths to generated assets
/// </summary>
public class MediaPreviewResult
{
    public string? ThumbnailPath { get; set; }
    public string? PreviewPath { get; set; }
    public MediaMetadata? Metadata { get; set; }
}