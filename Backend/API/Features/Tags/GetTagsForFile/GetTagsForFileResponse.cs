using Common;
using DTO;

namespace API.Features.Tags.GetTagsForFile;

public class GetTagsForFileResponse
{
    public Guid FileId { get; set; }
    public ICollection<TagDto> Tags { get; set; } = new List<TagDto>();
}