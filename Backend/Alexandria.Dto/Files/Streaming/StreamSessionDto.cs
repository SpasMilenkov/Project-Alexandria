using Alexandria.Data.Models;

namespace Alexandria.Dto.Files.Streaming;

public sealed class StreamSessionDto
{
    public Guid Id { get; init; }
    public Guid StreamHistoryId { get; init; }
    public long StartPositionSeconds { get; init; }
    public long EndPositionSeconds { get; init; }
    public long ListenedSeconds { get; init; }
    public bool ReachedCompletionThreshold { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? EndedAt { get; init; }

    public static StreamSessionDto FromEntity(StreamSession entity) => new()
    {
        Id = entity.Id,
        StreamHistoryId = entity.StreamHistoryId,
        StartPositionSeconds = entity.StartPositionSeconds,
        EndPositionSeconds = entity.EndPositionSeconds,
        ListenedSeconds = entity.ListenedSeconds,
        ReachedCompletionThreshold = entity.ReachedCompletionThreshold,
        StartedAt = entity.StartedAt,
        EndedAt = entity.EndedAt,
    };
}