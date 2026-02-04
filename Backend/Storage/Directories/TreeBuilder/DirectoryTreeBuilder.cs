namespace Storage.Directories.TreeBuilder;

public class DirectoryTreeBuilder
{
    public static DirectoryNode BuildDirectoryTree(List<string> paths, string rootName, Guid? rootId,
        bool isRoot = false)
    {
        var root = new DirectoryNode(rootName, "", null, rootId, isRoot);

        foreach (var path in paths)
        {
            var segments = path.Split('/');
            var currentNode = root;
            var currentPath = "";

            for (var i = 0; i < segments.Length - 1; i++)
            {
                var directoryName = segments[i];
                currentPath = string.IsNullOrEmpty(currentPath)
                    ? directoryName
                    : currentPath + "/" + directoryName;

                if (!currentNode.Children.TryGetValue(directoryName, out var value))
                {
                    value = new DirectoryNode(
                        directoryName,
                        currentPath,
                        currentNode.Id
                    );
                    currentNode.Children[directoryName] = value;
                }

                currentNode = value;
            }
        }

        return root;
    }

    public static Dictionary<string, Guid?> BuildFileToParentMapping(
        List<string> filePaths,
        DirectoryNode root)
    {
        var fileToParentGuid = new Dictionary<string, Guid?>();

        foreach (var filePath in filePaths)
        {
            var segments = filePath.Split('/');
            var currentNode = root;

            for (var i = 0; i < segments.Length - 1; i++)
            {
                var directoryName = segments[i];
                currentNode = currentNode.Children[directoryName];
            }

            fileToParentGuid[filePath] = currentNode.Id;
        }

        return fileToParentGuid;
    }

    public List<DirectoryNode> GetAllDirectories(DirectoryNode root)
    {
        var directories = new List<DirectoryNode>();
        CollectDirectories(root, directories);
        return directories;
    }

    private static void CollectDirectories(DirectoryNode node, List<DirectoryNode> collection)
    {
        collection.Add(node);
        foreach (var child in node.Children.Values)
        {
            CollectDirectories(child, collection);
        }
    }
}