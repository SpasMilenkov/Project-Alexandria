namespace API.Features.DownloadFile;

public class DownloadFileResponse
{
    public required IFormFile File { get; set; }
    public string? VersionId { get; set; }
}