using Alexandria.Dto.Files;

namespace Alexandria.Common.Services;

/// <summary>
/// The editor's working text between versions. Autosave writes the draft, which is cheap and
/// starts no background work; a checkpoint turns it into the next version.
/// </summary>
public interface IFileDraftService
{
    /// <summary>The file's draft, or null when it has none.</summary>
    /// <exception cref="Exceptions.Files.FileRecordNotFoundException">Missing, trashed or not owned.</exception>
    Task<FileDraftDto?> GetAsync(Guid fileId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Stores <paramref name="content"/> as the draft made from <paramref name="baseVersionId"/>.
    /// <paramref name="expectedRevision"/> is the revision this editor last saw, or null when it
    /// has seen none. Text equal to the base version removes the draft instead.
    /// </summary>
    /// <exception cref="Exceptions.FileVersions.FileVersionConflictException">The base is no longer current.</exception>
    /// <exception cref="Exceptions.Files.FileDraftConflictException">A draft exists at another revision.</exception>
    /// <exception cref="Exceptions.Files.FileRecordNotFoundException">Missing, trashed or not owned.</exception>
    /// <exception cref="Exceptions.Files.FileContentNotEditableException">
    /// The base is encrypted, not text, or too large to open; or the text is too large.
    /// </exception>
    Task<FileDraftSaveResult> SaveAsync(Guid fileId, Guid baseVersionId, Guid? expectedRevision, string content,
        Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Removes the draft if it is still at <paramref name="revision"/>. Returns false when there
    /// was no draft at that revision, which leaves a newer one from another editor alone.
    /// </summary>
    /// <exception cref="Exceptions.Files.FileRecordNotFoundException">Missing, trashed or not owned.</exception>
    Task<bool> DiscardAsync(Guid fileId, Guid revision, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Cuts the next version from <paramref name="content"/> and removes the draft at
    /// <paramref name="draftRevision"/>, then does what follows any new version: names a note
    /// still called "Untitled" and runs the folder's policies.
    /// </summary>
    /// <exception cref="Exceptions.FileVersions.FileVersionConflictException">The parent is no longer current.</exception>
    /// <exception cref="Exceptions.Files.FileDraftConflictException">A draft exists at another revision.</exception>
    /// <exception cref="Exceptions.Files.FileRecordNotFoundException">Missing, trashed or not owned.</exception>
    /// <exception cref="Exceptions.FileVersions.FileVersionNotFoundException">The parent isn't one of the file's versions.</exception>
    /// <exception cref="Exceptions.Files.FileContentNotEditableException">
    /// The parent is encrypted, not text, or too large to open; or the text is too large.
    /// </exception>
    Task<FileContentSaveResult> CheckpointAsync(Guid fileId, Guid parentVersionId, Guid? draftRevision,
        string content, Guid userId, CancellationToken ct = default);
}
