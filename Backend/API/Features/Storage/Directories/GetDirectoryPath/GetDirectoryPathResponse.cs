using DTO.Directories;

namespace API.Features.Storage.Directories.GetDirectoryPath;

public class GetDirectoryPathResponse
{
    public required List<PathPartDto> PathParts { get; set; }
}