using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Gallery;

/// <summary>
/// What a client asks for: the cursor is still the opaque string, decoded by the service.
/// </summary>
public sealed record GalleryPageFilter
{
    public required GallerySort Sort { get; init; }
    public string? Cursor { get; init; }
    public DateTime? Before { get; init; }
    public DateTime? Since { get; init; }
    public required int Limit { get; init; }
}