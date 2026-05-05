namespace Alexandria.Api.Features.Tags.CreateTag;

public sealed class CreateTagRequest
{
    public required string Name { get; set; }
    public required string Icon { get; set; }
    public required string Color { get; set; }
    public string? Description { get; set; }
}