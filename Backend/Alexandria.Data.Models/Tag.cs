using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Data.Models;

public class Tag : IBase
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Icon { get; set; }
    public required string Color { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Machine key matching the external taxonomy — the exact identifier(s) the
    /// model emits, e.g. the discogs genre label <c>"Rock---Nu Metal"</c> or the
    /// composite mood key <c>"mood_aggressive:aggressive"</c>. Null for user-created
    /// tags; unique wherever set. The tag-sync step maps incoming labels to tags via
    /// this key, never by parsing display names.
    /// </summary>
    public string? ExternalKey { get; set; }

    /// <summary>
    /// Derivation family this tag belongs to (<see cref="TagFacet"/>), populated only
    /// for system-seeded taxonomy tags. Used to scope sync operations to a facet and
    /// for frontend genre/mood filtering.
    /// </summary>
    public TagFacet? Facet { get; set; }

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