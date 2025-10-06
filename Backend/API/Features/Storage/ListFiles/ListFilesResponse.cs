namespace API.Features.Storage.ListFiles;

public class ListFilesResponse
{
    public List<FileInfoDto> Files { get; set; } = new();
}
