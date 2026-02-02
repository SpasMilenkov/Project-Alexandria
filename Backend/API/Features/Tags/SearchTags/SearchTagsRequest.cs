namespace API.Features.Tags.SearchTags;

public class SearchTagsRequest
{
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public DateTime? UpdatedAfter { get; set; }

    public DateTime? UpdatedBefore { get; set; }

    // if given will exclude tags already on file
    public Guid? ExcludeOnFile { get; set; }
    public string? NameContains { get; set; }
    public bool? HasFiles { get; set; }
    public int Page { get; set; } = 0;
    public int PageSize { get; set; } = 20;
}