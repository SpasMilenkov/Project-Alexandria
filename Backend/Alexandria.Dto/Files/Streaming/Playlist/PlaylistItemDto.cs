namespace Alexandria.Dto.Files.Streaming.Playlist;

public record PlaylistItemDto
{
    public Guid Id { get; init; }
    public int Position { get; init; }
    public Guid TranspilationJobId { get; init; }
    public string FileName { get; init; } = null!;
    public string MimeType { get; init; } = null!;
    public string? SegmentPrefix { get; init; }
    public IReadOnlyList<StreamingRepresentationDto> Representations { get; init; } = [];
    public DateTime CreatedAt { get; init; }
}