namespace Alexandria.Dto.Files.Streaming.Stats;

public sealed record JumpBackInResponse(
    int InactiveDays, DateTime InactiveBefore, IReadOnlyList<JumpBackInSongDto> Items);

public sealed record JumpBackInSongDto(
    MediaFileDto File, int QualifiedPlayCount, long TotalListenedSeconds, DateTime LastPlayedAt);
