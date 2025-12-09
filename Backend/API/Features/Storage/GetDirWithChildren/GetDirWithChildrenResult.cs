using DTO.Directories;

namespace API.Features.Storage.GetDirWithChildren;

public class GetDirWithChildrenResult
{
    public required DirectoryDto Directory { get; set; }
}