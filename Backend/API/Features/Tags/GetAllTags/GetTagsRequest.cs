namespace API.Features.Tags.GetAllTags;

public class GetTagsRequest
{
    public int Page { get; set; } = 0;
    public int PageSize { get; set; } = 20;
}
