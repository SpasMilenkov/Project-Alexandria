namespace Alexandria.Common.Exceptions.Directories;

public class DirectoryRestoreException : Exception
{
    public Guid[] DirectoryIds { get; }

    public DirectoryRestoreException(Guid[] directoryIds)
        : base("Restore failed: no matching directories were found within the 30-day retention window.")
    {
        DirectoryIds = directoryIds;
    }

    public DirectoryRestoreException(Guid[] directoryIds, string reason)
        : base($"Restore failed: {reason}")
    {
        DirectoryIds = directoryIds;
    }
}