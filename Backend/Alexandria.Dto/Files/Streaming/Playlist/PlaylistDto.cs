namespace Alexandria.Dto.Files.Streaming.Playlist;

public record PlaylistDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
    public bool HasCover { get; init; } = false;
    public string? AmbientTheme { get; init; }
    public int ItemCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public static PlaylistDto FromEntity(Data.Models.Playlist entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description,
        HasCover = entity.HasCover,
        AmbientTheme = entity.AmbientTheme,
        ItemCount = entity.PlaylistItems?.Count(i => i.DeletedAt == null) ?? 0,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}