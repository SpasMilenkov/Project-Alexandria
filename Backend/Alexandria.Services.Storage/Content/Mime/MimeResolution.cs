namespace Alexandria.Services.Storage.Content.Mime;

/// <summary>Which signal decided the resolved MIME type.</summary>
public enum MimeSource
{
    SniffedSpecific,
    Extension,
    SniffedGeneric,
    Declared,
    TextHeuristic,
    Fallback,
    FileName
}

/// <summary>
/// The outcome of MIME resolution. <paramref name="MimeType"/> is always canonical.
/// <paramref name="Sniffed"/> is the canonical sniffed type, or null when nothing was sniffed.
/// </summary>
public sealed record MimeResolution(string MimeType, MimeSource Source, string? Sniffed);
