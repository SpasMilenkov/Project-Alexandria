using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Common.Repositories;

public interface IPreviewJobRepository : IRepository<PreviewJob>
{
    Task<PreviewJob?> GetByJobIdAsync(Guid jobId, CancellationToken ct = default);

    /// <summary>
    /// The in-flight (Queued or Processing) job for a version/kind/owner, if any.
    /// Used at dispatch to enqueue genuinely new work only.
    /// </summary>
    Task<PreviewJob?> GetActiveJobForVersionAsync(
        Guid versionId, PreviewKind kind, Guid userId, CancellationToken ct = default);
}