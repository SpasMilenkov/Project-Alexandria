using Alexandria.Dto.Directories;
using Alexandria.Dto.Files;
using Alexandria.Dto.Notes;

namespace Alexandria.Common.Services;

/// <summary>
/// Notes are markdown files anywhere under the owner's Notes folder, a root folder marked as a
/// system folder so it is found by identity rather than by name.
/// </summary>
public interface INoteService
{
    /// <summary>The owner's Notes folder, or null when no note has been created yet.</summary>
    Task<DirectorySummaryDto?> GetNotesFolderAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Returns the owner's Notes folder, creating it at the root on first use. A root folder
    /// already named "Notes" is adopted instead of getting a "Notes 2" next to it.
    /// </summary>
    Task<DirectorySummaryDto> EnsureNotesFolderAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Creates an empty note named after <paramref name="title"/> ("Untitled" when blank), in
    /// <paramref name="directoryId"/> or the Notes folder itself. Taken names get a number.
    /// </summary>
    /// <exception cref="Exceptions.Notes.NotesFolderRequiredException">The folder is outside Notes.</exception>
    /// <exception cref="Exceptions.Directories.DirectoryNotFoundException">Missing, trashed or not owned.</exception>
    Task<FileContentSaveResult> CreateNoteAsync(Guid userId, Guid? directoryId, string? title,
        CancellationToken ct = default);

    /// <summary>
    /// For a note still called "Untitled", renames it after the first heading or line of
    /// <paramref name="text"/> once that line is finished, numbered when the folder already has
    /// that name. After that it is an ordinary name. Returns the note's name afterwards, or null
    /// when there was nothing to do. Never throws: the version is saved either way, and the next
    /// save tries again.
    /// </summary>
    Task<string?> NameUntitledNoteAsync(Guid fileId, string text, Guid userId, CancellationToken ct = default);

    /// <summary>Names the file after <paramref name="title"/>, keeping its extension.</summary>
    /// <exception cref="Exceptions.Files.FileRecordNotFoundException">Missing, trashed or not owned.</exception>
    /// <exception cref="Exceptions.Files.FileNameTakenException">Another file in the folder has that name.</exception>
    Task<NoteRenameResult> RenameAsync(Guid fileId, string title, Guid userId, CancellationToken ct = default);
}
