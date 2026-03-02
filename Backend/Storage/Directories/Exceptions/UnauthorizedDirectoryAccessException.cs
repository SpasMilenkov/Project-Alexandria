namespace Storage.Directories.Exceptions;

public class UnauthorizedDirectoryAccessException : Exception
{
    public Guid DirectoryId { get; }
    public Guid UserId { get; }

    public UnauthorizedDirectoryAccessException(Guid directoryId, Guid userId)
        : base($"User '{userId}' does not have access to directory '{directoryId}'.")
    {
        DirectoryId = directoryId;
        UserId = userId;
    }
}