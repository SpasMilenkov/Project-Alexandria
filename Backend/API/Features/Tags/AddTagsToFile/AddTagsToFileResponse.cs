namespace API.Features.Tags.AddTagsToFile;

public class AddTagsToFileResponse
{
    public Guid FileId { get; set; }
    public int TagsAdded { get; set; }
    public string Message { get; set; } = string.Empty;
}