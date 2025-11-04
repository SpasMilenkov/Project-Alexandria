using Common;
using DTO;

namespace API.Features.Tags.GetAllTags;

public class GetTagsResponse
{
    public ICollection<TagDto> Tags { get; set; } = new List<TagDto>();
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasPrevious { get; set; }
    public bool HasNext { get; set; }
}