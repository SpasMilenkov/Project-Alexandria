using Alexandria.Data.Models;

namespace Alexandria.Dto.Files.Streaming;

public sealed class StreamHistoryDto
{
    public Guid Id { get; init; }
    public Guid FileId { get; init; }
    public string Title { get; init; }
    public long PositionSeconds { get; init; }
    public long MaxPositionReachedSeconds { get; init; }
    public long TotalListenedSeconds { get; init; }
    public int TimesCompleted { get; init; }
    public DateTime? LastCompletedAt { get; init; }
    public DateTime LastAccessedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public static StreamHistoryDto FromEntity(StreamHistory entity) => new()
    {
        Id = entity.Id,
        FileId = entity.FileId,
        PositionSeconds = entity.PositionSeconds,
        MaxPositionReachedSeconds = entity.MaxPositionReachedSeconds,
        TotalListenedSeconds = entity.TotalListenedSeconds,
        TimesCompleted = entity.TimesCompleted,
        LastCompletedAt = entity.LastCompletedAt,
        LastAccessedAt = entity.LastAccessedAt,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
    };
}