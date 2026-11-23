using Alexandria.Common;
using Alexandria.Common.Exceptions.Files;
using Alexandria.Common.Exceptions.FileVersions;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Dto.Files;
using Alexandria.Services.Storage.Content;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Notes;

public sealed partial class FileDraftService(
    IUnitOfWork unitOfWork,
    IContentStorageService contentStorage,
    INoteService noteService,
    IPolicyDispatcher policyDispatcher,
    INoteLinkIndexService linkIndex,
    ILogger<FileDraftService> logger)
    : IFileDraftService
{
    public async Task<FileDraftDto?> GetAsync(Guid fileId, Guid userId, CancellationToken ct = default)
    {
        var owned = await unitOfWork.Files.ExistsAsync(
            f => f.Id == fileId && f.OwnerId == userId && f.DeletedAt == null, ct);

        if (!owned) throw new FileRecordNotFoundException(fileId);

        var draft = await unitOfWork.FileDrafts.GetByIdAsync(fileId, ct);

        if (draft is null) return null;

        return new FileDraftDto
        {
            FileId = draft.FileId,
            BaseVersionId = draft.BaseVersionId,
            Revision = draft.Revision,
            Content = draft.Content,
            UpdatedAt = draft.UpdatedAt ?? draft.CreatedAt,
        };
    }

    public async Task<FileDraftSaveResult> SaveAsync(Guid fileId, Guid baseVersionId, Guid? expectedRevision,
        string content, Guid userId, CancellationToken ct = default)
    {
        var hash = TextContentBytes.Hash(TextContentBytes.Encode(fileId, content));

        // Null when the text is back to the version and no draft is left.
        Guid? revision = null;
        var wasUntitled = false;

        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var (file, baseVersion) = await LockBaseVersionAsync(fileId, baseVersionId, userId, ct);
            var draft = await unitOfWork.FileDrafts.GetByIdAsync(fileId, ct);

            // Read under the lock already held, so a named note never takes a second one below.
            wasUntitled = NoteNames.IsUntitled(file.Name);

            if (draft is not null && draft.Revision != expectedRevision)
                throw new FileDraftConflictException(fileId, expectedRevision, draft.Revision);

            if (hash.AsSpan().SequenceEqual(baseVersion.ContentHash))
            {
                if (draft is not null) unitOfWork.FileDrafts.Remove(draft);
            }
            else if (draft is null)
            {
                revision = Guid.NewGuid();

                await unitOfWork.FileDrafts.AddAsync(new FileDraft
                {
                    FileId = fileId,
                    BaseVersionId = baseVersionId,
                    Revision = revision.Value,
                    Content = content,
                    UpdatedBy = userId,
                }, ct);
            }
            else
            {
                revision = Guid.NewGuid();

                draft.BaseVersionId = baseVersionId;
                draft.Revision = revision.Value;
                draft.Content = content;
                draft.UpdatedBy = userId;
                unitOfWork.FileDrafts.Update(draft);
            }

            await unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }

        // Naming follows the draft rather than waiting for a checkpoint, so the header keeps up
        // with the first line while it is being typed. Only a note that was still untitled can
        // take a name; the rename checks again under its own lock, so a name typed in the header
        // meanwhile still wins.
        string? renamedTo = null;

        if (revision is not null && wasUntitled)
            renamedTo = await noteService.NameUntitledNoteAsync(fileId, content, userId, ct);

        return new FileDraftSaveResult
        {
            Revision = revision,
            SavedAt = DateTime.UtcNow,
            RenamedTo = renamedTo,
        };
    }

    public async Task<bool> DiscardAsync(Guid fileId, Guid revision, Guid userId, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var file = await unitOfWork.Files.GetForUpdateAsync(fileId, userId, ct);

            if (file is null || file.DeletedAt is not null)
                throw new FileRecordNotFoundException(fileId);

            var draft = await unitOfWork.FileDrafts.GetByIdAsync(fileId, ct);
            var matches = draft is not null && draft.Revision == revision;

            if (matches) unitOfWork.FileDrafts.Remove(draft!);

            await unitOfWork.CommitAsync(ct);

            if (matches) LogDraftDiscarded(logger, fileId, revision);

            return matches;
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<FileContentSaveResult> CheckpointAsync(Guid fileId, Guid parentVersionId, Guid? draftRevision,
        string content, Guid userId, CancellationToken ct = default)
    {
        var saved = await contentStorage.SaveTextContentAsync(fileId, parentVersionId, draftRevision, content,
            userId, ct);

        if (saved.Unchanged) return saved;

        // The text is at hand, so the note's mentions show up elsewhere without waiting for the sweep.
        await linkIndex.IndexVersionAsync(saved.VersionId, content, ct);

        var name = await noteService.NameUntitledNoteAsync(saved.FileId, content, userId, ct);

        if (name is not null) saved = saved with { FileName = name };

        await DispatchFinalizedAsync(saved, userId, ct);

        return saved;
    }

    // The version is saved by now, so a policy problem must not report the save as failed: the
    // editor would retry from a parent that is no longer current and hit a conflict.
    private async Task DispatchFinalizedAsync(FileContentSaveResult saved, Guid userId, CancellationToken ct)
    {
        try
        {
            await policyDispatcher.DispatchAsync(new FileFinalizedEvent(saved.FileId,
                saved.VersionId,
                saved.DirectoryId,
                saved.MimeType,
                saved.FileName,
                userId,
                IsNewVersion: true), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogDispatchFailed(logger, ex, saved.FileId, saved.VersionId);
        }
    }

    // The file row lock orders draft writes against each other and against checkpoints, which
    // take the same lock before cutting a version.
    private async Task<(Data.Models.File File, FileVersion BaseVersion)> LockBaseVersionAsync(Guid fileId,
        Guid baseVersionId, Guid userId, CancellationToken ct)
    {
        var file = await unitOfWork.Files.GetForUpdateAsync(fileId, userId, ct);

        if (file is null || file.DeletedAt is not null)
            throw new FileRecordNotFoundException(fileId);

        if (file.CurrentVersionId != baseVersionId)
            throw new FileVersionConflictException(fileId, baseVersionId, file.CurrentVersionId);

        var baseVersion = await unitOfWork.FileVersions.GetByIdAsync(baseVersionId, ct)
                          ?? throw new FileVersionNotFoundException(baseVersionId);

        EditableText.EnsureEditable(baseVersion);

        return (file, baseVersion);
    }
}
