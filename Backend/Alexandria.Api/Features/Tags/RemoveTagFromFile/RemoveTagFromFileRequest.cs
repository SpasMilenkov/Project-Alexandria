namespace Alexandria.Api.Features.Tags.RemoveTagFromFile;

public class RemoveTagFromFileRequest
{
    public Guid FileId { get; set; }
    public Guid TagId { get; set; }
}