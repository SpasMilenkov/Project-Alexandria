using System.ComponentModel.DataAnnotations;

namespace API.Features.Storage.DownloadFile;

public class DownloadFileRequest(string name, string path)
{
    [Required] public string Name { get; } = name;
    [Required] public string Path { get; } = path;
}