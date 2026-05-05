namespace Alexandria.Common.Exceptions.Directories;

public class CircularDirectoryReferenceException : Exception
{
    public Guid DirectoryId { get; }
    public Guid ParentId { get; }

    public CircularDirectoryReferenceException(Guid directoryId, Guid parentId)
        : base($"Cannot set parent directory. This would create a circular reference.")
    {
        DirectoryId = directoryId;
        ParentId = parentId;
    }
}