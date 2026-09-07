namespace Alexandria.Api.Features.Storage.Previews.DeletePreviewsByFile;

public sealed class DeletePreviewsByFileRequest
{
    public Guid FileId { get; set; }
    public DateTimeOffset? CreatedBefore { get; set; }
}