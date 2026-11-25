using Alexandria.Data.Models;
using Alexandria.Dto.Outbox;

namespace Alexandria.Common.Repositories;

public interface IOutboxMessageRepository : IRepository<OutboxMessage>
{
    /// <summary>
    /// Locks up to <paramref name="batchSize"/> messages that are due, oldest first, skipping rows
    /// another relay already holds. The rows come back tracked and stay locked until the caller's
    /// transaction ends, so this must run inside one.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> ClaimDueAsync(DateTime now, int batchSize, CancellationToken ct = default);

    Task<OutboxSummaryDto> GetSummaryAsync(int recentDeadLimit, CancellationToken ct = default);

    /// <summary>When the oldest message still waiting to be sent was written; null when none wait.</summary>
    Task<DateTime?> GetOldestPendingCreatedAtAsync(CancellationToken ct = default);

    /// <summary>
    /// Makes every dead message due again with a fresh attempt budget. Its waiting time restarts
    /// from <paramref name="now"/>, so the backlog check judges the resend, not the first send.
    /// </summary>
    Task<int> ResendDeadAsync(DateTime now, CancellationToken ct = default);

    Task<int> DeleteSentBeforeAsync(DateTime sentBefore, CancellationToken ct = default);
}
