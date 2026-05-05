namespace Alexandria.Common.Exceptions.Directories;

public class DirectoryNotFoundException : Exception
{
    public Guid DirectoryId { get; }

    public DirectoryNotFoundException(Guid directoryId)
        : base($"Directory with ID '{directoryId}' was not found.")
    {
        DirectoryId = directoryId;
    }
}