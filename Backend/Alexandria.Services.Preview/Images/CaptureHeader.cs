namespace Alexandria.Services.Preview.Images;

/// <summary>
/// What an image header says about itself, before any interpretation.
/// </summary>
internal sealed record CaptureHeader
{
    public int? Width { get; init; }
    public int? Height { get; init; }
    public int? Orientation { get; init; }

    /// <summary>
    /// Raw header values by libvips field name, for example <c>exif-ifd2-FNumber</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string> Fields { get; init; } = new Dictionary<string, string>();
}
