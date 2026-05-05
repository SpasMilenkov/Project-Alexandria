using Alexandria.Dto.Directories;

namespace Alexandria.Api.Features.Storage.Directories.CreateDir;

public class CreateDirResult
{
    public required DirectorySummaryDto Directory { get; set; }
}