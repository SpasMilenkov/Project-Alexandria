namespace API.Features.Storage.Files.DownloadFile;

public class DownloadFileResponse
{
    public required IFormFile File { get; set; }
    public string? VersionId { get; set; }
}