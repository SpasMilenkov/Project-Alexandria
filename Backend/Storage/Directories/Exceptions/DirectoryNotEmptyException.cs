namespace Storage.Directories.Exceptions;

public class DirectoryNotEmptyException : Exception
{
    public Guid DirectoryId { get; }
    public int ItemCount { get; }
    
    public DirectoryNotEmptyException(Guid directoryId, int itemCount) 
        : base($"Directory with ID '{directoryId}' contains {itemCount} item(s) and cannot be deleted.")
    {
        DirectoryId = directoryId;
        ItemCount = itemCount;
    }
}