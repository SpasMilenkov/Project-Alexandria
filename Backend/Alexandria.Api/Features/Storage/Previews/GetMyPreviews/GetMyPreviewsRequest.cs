namespace Alexandria.Api.Features.Storage.Previews.GetMyPreviews;

public sealed class GetMyPreviewsRequest
{
    public Guid? FileId { get; set; }
    public DateTimeOffset? CreatedBefore { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}