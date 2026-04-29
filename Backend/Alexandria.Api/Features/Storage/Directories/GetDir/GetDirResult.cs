using Alexandria.Dto.Directories;

namespace Alexandria.Api.Features.Storage.Directories.GetDir;

public class GetDirResult
{
    public required DirectorySummaryDto Directory { get; set; }
}