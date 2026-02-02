namespace API.Features.Tags.UpdateTags;

public sealed class UpdateTagRequest
{
    public Guid TagId { get; set; }
    public string? Name { get; set; }
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public string? Description { get; set; }
}