namespace Alexandria.Dto.Files.Streaming.Playlist;

public record PlaylistDetailDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
    public bool HasCover { get; init; } = false;
    public string? AmbientTheme { get; init; }
    public IReadOnlyList<PlaylistItemDto> Items { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}