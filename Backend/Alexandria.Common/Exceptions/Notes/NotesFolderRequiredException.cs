namespace Alexandria.Common.Exceptions.Notes;

/// <summary>
/// Raised when a new note is asked for in a folder outside Notes. A markdown file there wouldn't
/// count as a note, so it would never show up in notes search.
/// </summary>
public sealed class NotesFolderRequiredException(Guid directoryId)
    : Exception("New notes go in the Notes folder or one of its subfolders.")
{
    public Guid DirectoryId { get; } = directoryId;
}
