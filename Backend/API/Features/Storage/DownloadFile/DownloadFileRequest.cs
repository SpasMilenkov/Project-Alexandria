using System.ComponentModel.DataAnnotations;

namespace API.Features.Storage.DownloadFile;

public class DownloadFileRequest
{
    [Required] public string Name { get; set; }
    [Required] public string Path { get; set; }
}