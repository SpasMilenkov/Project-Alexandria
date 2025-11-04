namespace API.Features.Tags.UpdateTags;

public class UpdateTagRequest
{
    public Guid TagId { get; set; }
    public required string Name { get; set; }
}
