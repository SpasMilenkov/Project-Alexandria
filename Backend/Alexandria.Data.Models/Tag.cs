namespace Alexandria.Data.Models;

public class Tag : IBase
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Icon { get; set; }
    public required string Color { get; set; }

    public string? Description { get; set; }

    // one level only, e.g. Nu Metal -> Rock. Null for user tags and flat facets like mood.
    public Guid? ParentId { get; set; }
    public Tag? Parent { get; set; }
    public List<Tag>? Children { get; set; }

    public List<FileTag>? FileTags { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public ApplicationUser? Owner { get; set; }
    public Guid OwnerId { get; set; }
}