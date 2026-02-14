namespace API.Features.Tags.CrteateTag;

public class CreateTagResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}