namespace Alexandria.Dto.Files.Streaming;

public sealed class MediaFileDto
{
    public Guid FileId { get; set; }
    public string FileName { get; set; }
    public string MimeType { get; set; }

    public Guid CurrentVersionId { get; set; }

    // public FileResult File { get; set; }
    public double? Duration { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? Title { get; set; }
    public Guid TranspilationJobId { get; set; }
    public bool IsVideo { get; set; }
    public string? SegmentPrefix { get; set; }
}