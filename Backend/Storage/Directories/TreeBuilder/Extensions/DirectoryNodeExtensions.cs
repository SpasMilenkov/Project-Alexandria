using Directory = Models.Directory;

namespace Storage.Directories.TreeBuilder.Extensions;

public static class DirectoryNodeExtensions
{
    public static Directory ToDirectory(this DirectoryNode node, Guid ownerId)
    {
        return new Directory
        {
            // The only time this is null should be when we are appending a dir subtree to the root
            // This should never be null if it the workflow is valid so we can cast safely here.
            Id = (Guid)node.Id!,
            Name = node.Name,
            ParentId = node.ParentId,
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static DirectoryNode ToDirectoryNode(this Directory directory)
    {
        return new DirectoryNode(
            name: directory.Name,
            fullPath: directory.Name,
            parentId: directory.ParentId
        )
        {
            Id = directory.Id
        };
    }
}