using DTO.Directories;

namespace API.Features.Storage.UpdateDir;

public class UpdateDirResponse
{
    public required DirectoryDto Directory { get; set; }
}