namespace Alexandria.Dto.Files.Streaming.Playlist;

public sealed record PlaylistFavoriteResponse(Guid PlaylistId, bool IsStarred);