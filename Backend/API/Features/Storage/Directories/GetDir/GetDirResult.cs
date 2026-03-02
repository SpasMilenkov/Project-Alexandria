using DTO.Directories;

namespace API.Features.Storage.Directories.GetDir;

public class GetDirResult
{
    public required DirectorySummaryDto Directory { get; set; }
}