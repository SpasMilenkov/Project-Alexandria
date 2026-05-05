namespace Alexandria.Common.Exceptions.Directories;

public class DirectoryMoveException : Exception
{
    public Guid[] DirectoryIds { get; }
    public Guid? DestinationId { get; }

    public DirectoryMoveException(Guid[] directoryIds, Guid? destinationId)
        : base(
            "Move failed: one or more directories were not found, or the operation would create a circular reference.")
    {
        DirectoryIds = directoryIds;
        DestinationId = destinationId;
    }

    public DirectoryMoveException(Guid[] directoryIds, Guid? destinationId, string reason)
        : base($"Move failed: {reason}")
    {
        DirectoryIds = directoryIds;
        DestinationId = destinationId;
    }
}