using System.Text.Unicode;

namespace Alexandria.Services.Storage.Content.Mime;

internal static class MimeTextHeuristic
{
    /// <summary>
    /// True when the header looks like UTF-8 text. Empty headers and headers containing a NUL byte
    /// are never text. Only a header that was cut at <see cref="MimeSniffing.HeaderBytes"/> may end
    /// inside a multibyte character, so only then up to 3 trailing bytes are trimmed before the
    /// UTF-8 check is retried. UTF-16 text contains NUL bytes and is treated as binary.
    /// </summary>
    public static bool LooksLikeText(byte[]? header)
    {
        if (header is null || header.Length == 0) return false;

        var span = header.AsSpan();
        if (span.IndexOf((byte)0) >= 0) return false;
        if (Utf8.IsValid(span)) return true;
        if (header.Length != MimeSniffing.HeaderBytes) return false;

        for (var trim = 1; trim <= 3; trim++)
            if (Utf8.IsValid(span[..^trim]))
                return true;

        return false;
    }
}
