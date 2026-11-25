namespace Alexandria.Dto.Outbox;

public sealed record OutboxSummaryDto
{
    public required int PendingCount { get; init; }

    /// <summary>When the oldest message still waiting to be sent was written; null when none wait.</summary>
    public DateTime? OldestPendingAt { get; init; }

    public required int DeadCount { get; init; }

    /// <summary>The most recent dead messages, newest first.</summary>
    public required IReadOnlyList<OutboxDeadMessageDto> RecentDead { get; init; }
}
