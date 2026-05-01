namespace Alexandria.Common.Exceptions.Directories;

public class InvalidDirectoryNameException : Exception
{
    public string DirectoryName { get; }

    public InvalidDirectoryNameException(string directoryName, string reason)
        : base($"Invalid directory name '{directoryName}': {reason}")
    {
        DirectoryName = directoryName;
    }
}