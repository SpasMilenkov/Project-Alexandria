namespace Storage.Directories.TreeBuilder;

public class DirectoryNode
{
    public Guid? Id { get; set; }
    public string Name { get; set; }
    public string FullPath { get; set; }
    public Guid? ParentId { get; set; }
    public Dictionary<string, DirectoryNode> Children { get; set; }

    public DirectoryNode(string name, string fullPath, Guid? parentId = null, Guid? id = null, bool isRoot = false)
    {
        if (isRoot)
            Id = null;
        else
            Id = id ?? Guid.NewGuid();

        Name = name;
        FullPath = fullPath;
        ParentId = parentId;
        Children = new Dictionary<string, DirectoryNode>();
    }
}