namespace Alexandria.Common.Exceptions.Directories;

public class DirectoryNotEmptyException(Guid directoryId, int itemCount)
    : Exception($"Directory with ID '{directoryId}' contains {itemCount} item(s) and cannot be deleted.")
{
    public Guid DirectoryId { get; } = directoryId;
    public int ItemCount { get; } = itemCount;
}