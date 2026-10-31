using Alexandria.Common.Mime;

namespace Alexandria.Common.Helpers;

/// <summary>
/// Central file-type gate for the auto-tag pipeline. Audio (<c>audio/*</c> plus
/// <c>application/ogg</c>) is supported today; image/document/video cases grow here as
/// those handlers land. The worker's extension map (<c>MimeTypeExtensions</c>) stays the
/// source of truth for staging file extensions.
/// </summary>
public static class AutoTagSupportedFileTypes
{
    /// <summary>The supported set, also exported to the frontend catalog.</summary>
    public static MimeMatcher Supported { get; } =
        new(prefixes: ["audio/"], types: ["application/ogg"]);

    /// <summary>
    /// True when the MIME type is routable to the audio enrichment pipeline. The type is
    /// canonicalized first, so playlist aliases such as audio/x-mpegurl (really text/x-m3u) are
    /// not mistaken for audio. Null, empty and unknown types are never supported.
    /// </summary>
    public static bool IsSupported(string? mimeType)
    {
        var canonical = MimeAliases.Canonicalize(mimeType);

        return canonical.Length > 0 && Supported.Matches(canonical);
    }
}