namespace Alexandria.Services.Storage.Content.Mime;

/// <summary>
/// Detects a MIME type from the leading bytes of a file. Implementations must never throw:
/// a failure to sniff is reported as <c>null</c> so that it can never reject an upload.
/// </summary>
public interface IMimeSniffer
{
    /// <summary>Best match for the header bytes, or null when nothing matches.</summary>
    string? Detect(byte[] header);
}
