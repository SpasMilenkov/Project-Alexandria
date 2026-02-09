namespace DTO.Files;

public class FileMetadata
{
    public Guid Id { get; set; }
    public Guid VersionId { get; set; }
    public required string FileName { get; set; }
    public byte[] ContentHash { get; set; } = [];
    public required string MimeTYpe { get; set; }
}