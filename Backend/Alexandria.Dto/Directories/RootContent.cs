using File = Alexandria.Data.Models.File;
using Directory = Alexandria.Data.Models.Directory;

namespace Alexandria.Dto.Directories;

public class RootContent
{
    public IEnumerable<Directory> Directories { get; set; } = new List<Directory>();
    public IEnumerable<File> Files { get; set; } = new List<File>();
}