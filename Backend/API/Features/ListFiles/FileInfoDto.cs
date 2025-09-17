namespace API.Features.ListFiles;

public class FileInfoDto
{
    public Guid Id { get; set; }
    public required string MimeType { get; set; }
    public bool HasPreview { get; set; }
    public required string Path { get; set; }
    public DateTime? PreviewGeneratedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public required string Name { get; init; }
    public long Size { get; set; }
    public DateTime? LastModified { get; set; }
    public required string UpdatedBy { get; set; }
    public bool IsDir { get; set; }
}