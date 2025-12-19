using DTO.Directories;

namespace API.Features.Storage.Directories.GetRootDir;

public class GetRootDirResponse
{
    public required RootContentSummaryDto RootContent { get; set; }
}