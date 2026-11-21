using Alexandria.Dto.DocumentTextStats;

namespace Alexandria.Common.Services;

public interface IDocumentTextStatsService
{
    /// <summary>
    /// Outcome counts, documents still waiting and the most recent failures across every account.
    /// Failures name only <paramref name="viewerId"/>'s own files.
    /// </summary>
    Task<DocumentTextOverviewResponse> GetOverviewAsync(Guid viewerId, CancellationToken ct);

    /// <summary>
    /// Gives every failed document a fresh set of attempts, starting with the sweep's next cycle.
    /// Returns how many documents were queued.
    /// </summary>
    Task<int> RetryFailedAsync(CancellationToken ct);
}