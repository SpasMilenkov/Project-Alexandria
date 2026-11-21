namespace Alexandria.Common.Exceptions.Files;

/// <summary>
/// Raised when creating a file whose name is already used in the folder, trashed files included,
/// because an upload with that name would turn into a new version of the existing file.
/// </summary>
public sealed class FileNameTakenException(string fileName, Guid? directoryId)
    : Exception($"A file named '{fileName}' already exists in this folder.")
{
    public string FileName { get; } = fileName;
    public Guid? DirectoryId { get; } = directoryId;
}
