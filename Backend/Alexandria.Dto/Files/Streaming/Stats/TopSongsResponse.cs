namespace Alexandria.Dto.Files.Streaming.Stats;

public sealed record TopSongsResponse(DateTime From, DateTime To, IReadOnlyList<TopSongDto> Items);

public sealed record TopSongDto(MediaFileDto File, int QualifiedPlayCount, long TotalListenedSeconds);
