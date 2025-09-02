namespace API.Features.ListFiles;

public class ListFilesResponse
{
    public List<FileInfoDto> Files { get; set; } = new();
}
