namespace API.Features.Tags.SearchFileByTags;

public class SearchFilesByTagsResponse
{
    public ICollection<FileDto> Files { get; set; } = new List<FileDto>();
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasPrevious { get; set; }
    public bool HasNext { get; set; }
}