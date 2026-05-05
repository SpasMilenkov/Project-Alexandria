using Alexandria.Dto.Directories;

namespace Alexandria.Api.Features.Storage.Directories.GetRootDir;

public class GetRootDirResponse
{
    public required RootContentSummaryDto RootContent { get; set; }
}