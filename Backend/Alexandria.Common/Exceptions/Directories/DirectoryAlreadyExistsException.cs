namespace Alexandria.Common.Exceptions.Directories;

public class DirectoryAlreadyExistsException : Exception
{
    public string DirectoryName { get; }
    public Guid? ParentId { get; }

    public DirectoryAlreadyExistsException(string directoryName, Guid? parentId)
        : base($"Directory '{directoryName}' already exists in the specified location.")
    {
        DirectoryName = directoryName;
        ParentId = parentId;
    }
}