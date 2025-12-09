using DTO.Directories;

namespace API.Features.Storage.GetRootDir;

public class GetRootDirResponse
{
    public required RootContentSummaryDto RootContent { get; set; }
}