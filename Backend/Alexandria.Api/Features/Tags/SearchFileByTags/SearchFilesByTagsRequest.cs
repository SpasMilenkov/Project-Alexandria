namespace Alexandria.Api.Features.Tags.SearchFileByTags;

public class SearchFilesByTagsRequest
{
    public required ICollection<Guid> TagIds { get; set; }
    public string MatchType { get; set; } = "any"; // "any", "all", "exact"
    public int Page { get; set; } = 0;
    public int PageSize { get; set; } = 20;
    public long? MinFileSize { get; set; }
    public long? MaxFileSize { get; set; }
    public string? MimeTypePrefix { get; set; }
    public DateTimeOffset? CreatedAfter { get; set; }
    public DateTimeOffset? CreatedBefore { get; set; }
}
