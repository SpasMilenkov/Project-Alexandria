namespace Alexandria.Api.Features.Tags.AddTagsToFile;

public class AddTagsToFileRequest
{
    public Guid FileId { get; set; }
    public required ICollection<Guid> TagIds { get; set; }
}