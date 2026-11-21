namespace Alexandria.Data.Models.Enumerators;

/// <summary>
/// Outcome of reading a document's body text. Every value except <see cref="Failed"/> is final
/// for its extractor version, so the sweep never reads that content again.
/// </summary>
public enum DocumentTextStatus
{
    Extracted = 0,

    /// <summary>
    /// The document was read and holds no text, like a blank page or an empty sheet.
    /// </summary>
    Empty = 1,

    /// <summary>
    /// A PDF with pages but next to no text, which almost always means scanned images.
    /// Finding words in it would need OCR.
    /// </summary>
    NoTextLayer = 2,

    PasswordProtected = 3,

    /// <summary>
    /// Reading threw. Retried later, up to a limit, because the cause may be temporary.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// A book whose chapters a store's copy protection encrypts. Unlike a password, no input
    /// from the owner could unlock it here.
    /// </summary>
    CopyProtected = 5
}