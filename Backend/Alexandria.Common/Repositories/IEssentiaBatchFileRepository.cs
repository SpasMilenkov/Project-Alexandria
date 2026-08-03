using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Common.Repositories;

public interface IEssentiaBatchFileRepository : IRepository<EssentiaBatchFile>
{
    /// <summary>
    /// Returns the batch-files for the given batch.
    /// </summary>
    Task<IEnumerable<EssentiaBatchFile>> GetByBatchAsync(Guid batchId, CancellationToken ct = default);

    /// <summary>
    /// Atomically transitions the batch-files for <paramref name="batchId"/> whose current status matches
    /// <paramref name="currentStatus"/> to <paramref name="newStatus"/>, stamping <c>UpdatedAt</c>.
    /// Returns the number of rows transitioned.
    /// </summary>
    Task<int> UpdateStatusForBatchAsync(
        Guid batchId,
        EssentiaBatchFileStatus currentStatus,
        EssentiaBatchFileStatus newStatus,
        Guid? updatedBy,
        CancellationToken ct = default);
}