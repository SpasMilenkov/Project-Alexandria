namespace API.Features.ListFiles;

public class FileInfoDto
{
    public string Name { get; set; } = null!;
    public long Size { get; set; }
    public DateTime? LastModified { get; set; }
    public bool IsDir { get; set; }
}