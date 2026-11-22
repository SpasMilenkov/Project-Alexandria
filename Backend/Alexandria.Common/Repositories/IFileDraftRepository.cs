using Alexandria.Data.Models;
using Alexandria.Dto.Files;

namespace Alexandria.Common.Repositories;

/// <summary>Drafts are keyed by file id, so <see cref="IRepository{T}.GetByIdAsync"/> takes one.</summary>
public interface IFileDraftRepository : IRepository<FileDraft>
{
    /// <summary>
    /// The revision of the file's draft, or null when it has none. Reads the column alone, so it
    /// leaves nothing tracked that a later locked read of the draft would be handed instead.
    /// </summary>
    Task<Guid?> GetRevisionAsync(Guid fileId, CancellationToken ct = default);

    /// <summary>
    /// Drafts last written before <paramref name="writtenBefore"/> that can still become a version:
    /// their file is live and they build on its current version. Oldest first, without their text.
    /// </summary>
    Task<IReadOnlyList<IdleFileDraft>> GetIdleAsync(DateTime writtenBefore, int limit,
        CancellationToken ct = default);

    /// <summary>
    /// The draft's text if it is still at <paramref name="revision"/>, otherwise null. Untracked for
    /// the same reason as <see cref="GetRevisionAsync"/>: a checkpoint reads the draft again under
    /// the row lock and must see the row as it is then.
    /// </summary>
    Task<string?> GetContentAsync(Guid fileId, Guid revision, CancellationToken ct = default);
}
