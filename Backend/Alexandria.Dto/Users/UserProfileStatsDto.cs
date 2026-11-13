namespace Alexandria.Dto.Users;

/// <summary>Totals about the signed-in user's own library, shown on their profile.</summary>
public record UserProfileStatsDto
{
    public required int Files { get; init; }
    public required int Folders { get; init; }
    public required int Photos { get; init; }
    public required int Tracks { get; init; }
    public required int Videos { get; init; }
    public required int Playlists { get; init; }
    public required int Tags { get; init; }
    public required int ActiveShareLinks { get; init; }
    public required long ListenedSeconds { get; init; }
    public required int Plays { get; init; }
}
