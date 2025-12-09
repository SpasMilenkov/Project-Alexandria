using DTO.Directories;

namespace API.Features.Storage.CreateDir;

public class CreateDirResult
{
    public required DirectorySummaryDto Directory { get; set; }
}