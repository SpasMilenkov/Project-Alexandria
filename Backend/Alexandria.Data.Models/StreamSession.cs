namespace Alexandria.Data.Models;

public class StreamSession : IBase
{
    public Guid Id { get; set; }
    public Guid StreamHistoryId { get; set; }
    public StreamHistory StreamHistory { get; set; } = null!;

    public long StartPositionSeconds { get; set; }
    public long EndPositionSeconds { get; set; }

    // real playback time, not wall-clock (end - start); seeks excluded
    public long ListenedSeconds { get; set; }

    public bool ReachedCompletionThreshold { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}