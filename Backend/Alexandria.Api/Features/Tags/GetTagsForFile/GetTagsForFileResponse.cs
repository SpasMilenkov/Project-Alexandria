using Alexandria.Dto.Tags;

namespace Alexandria.Api.Features.Tags.GetTagsForFile;

public class GetTagsForFileResponse
{
    public Guid FileId { get; set; }
    public ICollection<TagDto> Tags { get; set; } = new List<TagDto>();
}