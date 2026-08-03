using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Common.Repositories;

public interface IEssentiaBatchRepository : IRepository<EssentiaBatch>
{
    /// <summary>
    /// Returns the batch with the given identifier, eagerly loading its batch-files.
    /// </summary>
    Task<EssentiaBatch?> GetWithFilesAsync(Guid batchId, CancellationToken ct = default);

    /// <summary>
    /// Returns all dispatched batches that were dispatched before the given cutoff
    /// (candidates for the timeout sweep).
    /// </summary>
    Task<IEnumerable<EssentiaBatch>> GetDispatchedSinceAsync(DateTime cutoff, CancellationToken ct = default);

    /// <summary>
    /// Atomically transitions all batches in <paramref name="batchIds"/> from
    /// <see cref="EssentiaBatchStatus.Dispatched"/> to <see cref="EssentiaBatchStatus.TimedOut"/>.
    /// Returns the number of batches transitioned.
    /// </summary>
    Task<int> MarkTimedOutAsync(IEnumerable<Guid> batchIds, CancellationToken ct = default);
}