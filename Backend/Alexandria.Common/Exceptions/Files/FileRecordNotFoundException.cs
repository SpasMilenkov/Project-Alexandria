namespace Alexandria.Common.Exceptions.Files;

/// <summary>The file doesn't exist, is in the trash, or belongs to someone else.</summary>
public sealed class FileRecordNotFoundException(Guid fileId)
    : Exception($"File {fileId} was not found.")
{
    public Guid FileId { get; } = fileId;
}
