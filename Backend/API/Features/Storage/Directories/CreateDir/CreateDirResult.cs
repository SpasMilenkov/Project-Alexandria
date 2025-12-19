using DTO.Directories;

namespace API.Features.Storage.Directories.CreateDir;

public class CreateDirResult
{
    public required DirectorySummaryDto Directory { get; set; }
}