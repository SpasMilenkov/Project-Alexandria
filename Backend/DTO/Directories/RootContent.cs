using File = Models.File;
using Directory = Models.Directory;

namespace DTO.Directories;

public class RootContent
{
    public IEnumerable<Directory> Directories { get; set; } = new List<Directory>();
    public IEnumerable<File> Files { get; set; } = new List<File>();
}
