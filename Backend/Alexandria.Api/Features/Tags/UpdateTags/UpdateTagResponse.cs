namespace Alexandria.Api.Features.Tags.UpdateTags;

public class UpdateTagResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}