using DTO.Directories;

namespace API.Features.Storage.Directories.UpdateDir;

public class UpdateDirResponse
{
    public required DirectoryDto Directory { get; set; }
}