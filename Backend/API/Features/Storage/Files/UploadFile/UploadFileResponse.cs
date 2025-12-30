namespace API.Features.Storage.Files.UploadFile;

public class UploadFileResponse
{
    public string ObjectName { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
    public Guid VersionId { get; set; }
    public long Size { get; set; }
    public Guid FileId { get; set; }
}