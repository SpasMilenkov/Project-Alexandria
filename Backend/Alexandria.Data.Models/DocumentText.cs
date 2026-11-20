using Alexandria.Data.Models.Enumerators;
using NpgsqlTypes;

namespace Alexandria.Data.Models;

/// <summary>
/// The body text of a stored document, used for full-text search. Kept per content object
/// rather than per version because text depends only on the bytes, so copies and identical
/// uploads share one extraction. A missing row means the content was not read yet, or cannot be
/// read because it is encrypted by its owner.
/// </summary>
public class DocumentText : IBase
{
    public Guid Id { get; set; }

    public Guid ContentObjectId { get; set; }
    public ContentObject? ContentObject { get; set; }

    public DocumentTextStatus Status { get; set; }

    /// <summary>
    /// The extracted text with whitespace collapsed. Page breaks stay as form feeds so page-level
    /// matching can be added without reading every document again. Empty unless extraction
    /// succeeded at some extractor version.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Written by the repository in SQL rather than computed by the column, because a vector
    /// over 1 MB is rejected and the write has to retry on a shorter prefix.
    /// </summary>
    public NpgsqlTsVector SearchVector { get; set; } = null!;

    public int CharacterCount { get; set; }

    /// <summary>
    /// True when search only covers the start of the document: the text was longer than the
    /// stored limit, or too varied to index whole.
    /// </summary>
    public bool IsTruncated { get; set; }

    /// <summary>
    /// Why the last attempt failed. Cleared by a successful extraction.
    /// </summary>
    public string? ErrorDetail { get; set; }

    /// <summary>
    /// Consecutive failed attempts at the current extractor version.
    /// </summary>
    public int AttemptCount { get; set; }

    public DateTime? LastAttemptAt { get; set; }

    /// <summary>
    /// Which extraction logic wrote this row. Raising the current version makes the sweep
    /// process the content again.
    /// </summary>
    public int ExtractorVersion { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}