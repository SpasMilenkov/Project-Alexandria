using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Tags;

public class TagDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Icon { get; set; }
    public required string Color { get; set; }
    public string? Description { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public TagSource Source { get; set; }
    public double? Confidence { get; set; }

    /// <summary>True when the tag belongs to the system account (seeded vocabulary).</summary>
    public bool IsSystem { get; set; }

    /// <summary>Derivation family for system-seeded tags; null for user tags.</summary>
    public TagFacet? Facet { get; set; }

    /// <summary>Parent tag id for subgenres (one level only); null otherwise.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Display name of the parent tag, when present.</summary>
    public string? ParentName { get; set; }
}