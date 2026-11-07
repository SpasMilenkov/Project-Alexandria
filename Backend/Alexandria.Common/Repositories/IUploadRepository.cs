using Alexandria.Data.Models;

namespace Alexandria.Common.Repositories;

public interface IUploadRepository : IRepository<Upload>
{
    /// <summary>
    /// Batch lookup for the temp sweeper. Returns live rows for the given temp
    /// object keys. Tracked, so the sweeper can flip abandoned rows to
    /// <c>Interrupted</c> in the same scope.
    /// </summary>
    Task<List<Upload>> GetByTempKeysAsync(IEnumerable<string> tempKeys, CancellationToken ct = default);

    /// <summary>
    /// Atomically moves an in-progress upload owned by the user to <c>Finalizing</c>.
    /// Returns false when another finalize already holds it or it is not finalizable, so
    /// a second call can never seal different bytes over an upload that passed verification.
    /// </summary>
    Task<bool> TryClaimForFinalizeAsync(Guid uploadId, Guid userId, DateTime now, CancellationToken ct = default);

    /// <summary>
    /// Ends a claimed upload as <c>Interrupted</c>. Writes directly to the database, because
    /// the caller's change tracker may still hold entities from a rolled-back commit.
    /// </summary>
    Task MarkInterruptedAsync(Guid uploadId, DateTime now, CancellationToken ct = default);

    /// <summary>Returns which of the user's uploads already reached <c>Finished</c>.</summary>
    Task<HashSet<Guid>> GetFinishedIdsAsync(
        IReadOnlyCollection<Guid> uploadIds,
        Guid userId,
        CancellationToken ct = default);
}