using System.ComponentModel.DataAnnotations;

namespace API.Features.DownloadFile;

public class DownloadFileRequest
{
    [Required] public string Name { get; set; }
    [Required] public string Path { get; set; }
}