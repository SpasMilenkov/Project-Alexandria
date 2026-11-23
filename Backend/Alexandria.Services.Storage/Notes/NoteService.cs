using Alexandria.Common;
using Alexandria.Common.Exceptions.Directories;
using Alexandria.Common.Exceptions.Files;
using Alexandria.Common.Exceptions.Notes;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Directories;
using Alexandria.Dto.Files;
using Alexandria.Dto.Notes;
using Microsoft.Extensions.Logging;
using Directory = Alexandria.Data.Models.Directory;
using DirectoryNotFoundException = Alexandria.Common.Exceptions.Directories.DirectoryNotFoundException;

namespace Alexandria.Services.Storage.Notes;

public sealed partial class NoteService(
    IUnitOfWork unitOfWork,
    IDirectoryService directoryService,
    IContentStorageService contentStorage,
    INoteLinkIndexService linkIndex,
    ILogger<NoteService> logger)
    : INoteService
{
    public const string NotesFolderName = "Notes";

    // Names are re-read between attempts, so this only runs out when several creates in the same
    // folder keep colliding.
    private const int CreateAttempts = 3;

    public async Task<DirectorySummaryDto?> GetNotesFolderAsync(Guid userId, CancellationToken ct = default)
    {
        var folder = await FindNotesFolderAsync(userId, ct);

        if (folder is null) return null;

        return await directoryService.GetDirectoryDtoByIdAsync(folder.Id, userId, ct);
    }

    public async Task<DirectorySummaryDto> EnsureNotesFolderAsync(Guid userId, CancellationToken ct = default)
    {
        var folderId = await EnsureNotesFolderIdAsync(userId, ct);

        return await directoryService.GetDirectoryDtoByIdAsync(folderId, userId, ct);
    }

    public async Task<FileContentSaveResult> CreateNoteAsync(Guid userId, Guid? directoryId, string? title,
        string? content, CancellationToken ct = default)
    {
        var text = content ?? string.Empty;

        var notesFolderId = await EnsureNotesFolderIdAsync(userId, ct);
        var targetId = directoryId ?? notesFolderId;

        if (targetId != notesFolderId)
            await EnsureInsideNotesAsync(targetId, notesFolderId, userId, ct);

        var cleanTitle = NoteNames.CleanTitle(title);

        for (var attempt = 1; ; attempt++)
        {
            var taken = await TakenNamesAsync(targetId, exceptFileId: null, userId, ct);
            var name = NoteNames.FirstFreeName(cleanTitle, taken);

            try
            {
                var created = await contentStorage.CreateTextFileAsync(name, targetId, text, userId, ct);

                LogNoteCreated(logger, created.FileId, targetId, userId);

                if (text.Length > 0) await linkIndex.IndexVersionAsync(created.VersionId, text, ct);

                return created;
            }
            catch (FileNameTakenException ex) when (attempt < CreateAttempts)
            {
                LogNoteNameTaken(logger, ex, name, targetId);
            }
        }
    }

    private Task<Directory?> FindNotesFolderAsync(Guid userId, CancellationToken ct) =>
        unitOfWork.Directories.FirstOrDefaultAsync(
            d => d.OwnerId == userId && d.SystemKind == DirectorySystemKind.Notes, ct);

    private async Task<Guid> EnsureNotesFolderIdAsync(Guid userId, CancellationToken ct)
    {
        var existing = await FindNotesFolderAsync(userId, ct);

        if (existing is not null) return existing.Id;

        try
        {
            return await CreateOrAdoptNotesFolderAsync(userId, ct);
        }
        catch (Exception ex)
        {
            // Two first notes at once: the unique index lets one folder through and the other
            // request uses it. Clearing the context drops the rejected row before reading again.
            await unitOfWork.RollbackAsync(CancellationToken.None);

            var winner = await FindNotesFolderAsync(userId, ct);

            if (winner is null) throw;

            LogNotesFolderRaced(logger, ex, userId);

            return winner.Id;
        }
    }

    private async Task<Guid> CreateOrAdoptNotesFolderAsync(Guid userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var named = await unitOfWork.Directories.FirstOrDefaultAsync(
            d => d.OwnerId == userId && d.ParentId == null && d.Name == NotesFolderName && d.DeletedAt == null, ct);

        if (named is not null)
        {
            named.SystemKind = DirectorySystemKind.Notes;
            named.UpdatedAt = now;
            named.UpdatedBy = userId;

            unitOfWork.Directories.Update(named);
            await unitOfWork.SaveChangesAsync(ct);

            LogNotesFolderAdopted(logger, named.Id, userId);

            return named.Id;
        }

        var folder = new Directory
        {
            Id = Guid.NewGuid(),
            Name = NotesFolderName,
            OwnerId = userId,
            ParentId = null,
            CreatedAt = now,
            SystemKind = DirectorySystemKind.Notes,
        };

        await unitOfWork.Directories.AddAsync(folder, ct);
        await unitOfWork.SaveChangesAsync(ct);

        LogNotesFolderCreated(logger, folder.Id, userId);

        return folder.Id;
    }

    private async Task EnsureInsideNotesAsync(Guid directoryId, Guid notesFolderId, Guid userId,
        CancellationToken ct)
    {
        var exists = await unitOfWork.Directories.ExistsAsync(
            d => d.Id == directoryId && d.OwnerId == userId && d.DeletedAt == null, ct);

        if (!exists) throw new DirectoryNotFoundException(directoryId);

        // The path runs from the root down, so its first folder says which tree this one is in.
        var path = await unitOfWork.Directories.GetDirectoryPathAsync(directoryId, ct);

        if (path.Count == 0 || path[0].Id != notesFolderId)
            throw new NotesFolderRequiredException(directoryId);
    }

    public async Task<IReadOnlyList<NoteSummaryDto>> GetSummariesAsync(IReadOnlyCollection<Guid> fileIds,
        Guid userId, CancellationToken ct = default)
    {
        var requested = fileIds.Distinct().ToList();

        // The unit of work shares one context, so the two reads run one after the other.
        var texts = await unitOfWork.DocumentText.GetLeadingTextAsync(requested, userId,
            NoteExcerpts.SourceLength, ct);
        var counts = await unitOfWork.NoteLinks.CountBacklinksAsync(LinkTargetKind.File, requested, userId, ct);

        var excerpts = texts.ToDictionary(t => t.FileId, t => NoteExcerpts.From(t.Text, t.Name));
        var mentions = counts.ToDictionary(c => c.TargetId, c => c.Count);

        return requested
            .Select(id => new NoteSummaryDto
            {
                FileId = id,
                Excerpt = excerpts.GetValueOrDefault(id),
                MentionCount = mentions.GetValueOrDefault(id),
            })
            .ToList();
    }

    public async Task<string?> NameUntitledNoteAsync(Guid fileId, string text, Guid userId,
        CancellationToken ct = default)
    {
        var title = NoteTitles.FromMarkdown(text);

        if (title is null) return null;

        try
        {
            var renamed = await RenameLockedAsync(fileId, title, userId, fromText: true, ct);

            return renamed?.FileName;
        }
        catch (Exception ex)
        {
            LogUntitledNameFailed(logger, ex, fileId);

            return null;
        }
    }

    public async Task<NoteRenameResult> RenameAsync(Guid fileId, string title, Guid userId,
        CancellationToken ct = default)
    {
        var renamed = await RenameLockedAsync(fileId, title, userId, fromText: false, ct);

        // Only a name taken from the text can step aside, so a rename by hand always has a result.
        return renamed ?? throw new InvalidOperationException($"Renaming file {fileId} settled on no name.");
    }

    // The row lock orders this against saves and other renames of the same file, so a name typed
    // in the header that lands first is never replaced by one read from the text.
    private async Task<NoteRenameResult?> RenameLockedAsync(Guid fileId, string title, Guid userId,
        bool fromText, CancellationToken ct)
    {
        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var file = await unitOfWork.Files.GetForUpdateAsync(fileId, userId, ct);

            if (file is null || file.DeletedAt is not null)
                throw new FileRecordNotFoundException(fileId);

            // The text only names notes nobody has named yet.
            if (fromText && !NoteNames.IsUntitled(file.Name))
            {
                await unitOfWork.CommitAsync(ct);
                return null;
            }

            var name = await NameForAsync(file, title, userId, fromText, ct);

            if (name != file.Name)
            {
                file.Name = name;
                file.UpdatedBy = userId;
                unitOfWork.Files.Update(file);

                LogNoteRenamed(logger, fileId, fromText);
            }

            await unitOfWork.CommitAsync(ct);

            return new NoteRenameResult(file.Id, file.Name);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    // A name read from the text gets the next free number when taken; a name typed by hand is
    // refused instead, since numbering it would quietly change what was typed.
    private async Task<string> NameForAsync(Data.Models.File file, string title, Guid userId, bool fromText,
        CancellationToken ct)
    {
        var cleanTitle = NoteNames.CleanTitle(title);
        var extension = NoteNames.ExtensionOf(file.Name);
        var taken = await TakenNamesAsync(file.DirectoryId, file.Id, userId, ct);

        if (fromText) return NoteNames.FirstFreeName(cleanTitle, taken, extension);

        var name = cleanTitle + extension;

        if (taken.Contains(name)) throw new FileNameTakenException(name, file.DirectoryId);

        return name;
    }

    // Trashed files count, because an upload with a trashed file's name adds a version to it.
    private async Task<HashSet<string>> TakenNamesAsync(Guid? directoryId, Guid? exceptFileId, Guid userId,
        CancellationToken ct)
    {
        var files = await unitOfWork.Files.FindAsync(
            f => f.DirectoryId == directoryId && f.OwnerId == userId && f.Id != exceptFileId, ct);

        return files.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
