using DTO.Directories;

namespace API.Features.Storage.Directories.GetDirWithChildren;

public class GetDirWithChildrenResult
{
    public required DirectoryDto Directory { get; set; }
}