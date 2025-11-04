using Common;
using DTO;

namespace API.Features.Tags.SearchFileByTags;

public class FileDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool HasPreview { get; set; }
    public ICollection<TagDto> Tags { get; set; } = new List<TagDto>();
}