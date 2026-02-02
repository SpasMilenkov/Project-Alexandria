using DTO.Tags;

namespace API.Features.Tags.SearchTags;

public class SearchTagsResponse
{
    public ICollection<TagDto> Tags { get; set; } = new List<TagDto>();
    public required int CurrentPage { get; set; }
    public required int PageSize { get; set; }
    public required int TotalCount { get; set; }
    public required int TotalPages { get; set; }
    public required bool HasPrevious { get; set; }
    public required bool HasNext { get; set; }
}