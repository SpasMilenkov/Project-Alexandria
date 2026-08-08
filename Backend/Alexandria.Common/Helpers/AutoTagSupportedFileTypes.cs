namespace Alexandria.Common.Helpers;

/// <summary>
/// Central file-type gate for the auto-tag pipeline. Audio (<c>audio/*</c> plus
/// <c>application/ogg</c>) is supported today; image/document/video cases grow here as
/// those handlers land. The worker's extension map (<c>MimeTypeExtensions</c>) stays the
/// source of truth for staging file extensions.
/// </summary>
public static class AutoTagSupportedFileTypes
{
    /// <summary>
    /// True when the MIME type is routable to the audio enrichment pipeline.
    /// Null, empty and unknown types are never supported.
    /// </summary>
    public static bool IsSupported(string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
            return false;

        var normalized = mimeType.Trim().ToLowerInvariant();

        return normalized.StartsWith("audio/", StringComparison.Ordinal) ||
               normalized == "application/ogg";
    }
}