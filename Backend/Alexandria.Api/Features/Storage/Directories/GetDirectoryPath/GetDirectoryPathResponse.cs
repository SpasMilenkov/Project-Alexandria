using Alexandria.Dto.Directories;

namespace Alexandria.Api.Features.Storage.Directories.GetDirectoryPath;

public class GetDirectoryPathResponse
{
    public required List<PathPartDto> PathParts { get; set; }
}