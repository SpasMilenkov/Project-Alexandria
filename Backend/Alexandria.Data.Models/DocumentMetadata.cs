namespace Alexandria.Data.Models;

/// <summary>
/// The properties a document version carries about itself: who wrote it, with what, and when.
/// Kept per version because a share link can pin an older version whose author differs.
/// A row with empty fields means the version was read and carries nothing, which stops the
/// backfill from reading it again. A missing row means it was not read yet, or cannot be read
/// because it is encrypted by Alexandria.
/// </summary>
public class DocumentMetadata : IBase
{
    public Guid Id { get; set; }

    public Guid FileVersionId { get; set; }
    public FileVersion? FileVersion { get; set; }

    public string? Title { get; set; }
    public string? Subject { get; set; }
    public string? Keywords { get; set; }
    public string? Language { get; set; }

    /// <summary>
    /// Names stored by the authoring app, often the account name of whoever set it up. Never
    /// shown outside the owner's own view, because a shared copy still carries them.
    /// </summary>
    public string? Author { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? Company { get; set; }

    /// <summary>
    /// The app the document was written in. For a PDF this is the original app, and
    /// <see cref="Producer"/> is whatever turned it into a PDF.
    /// </summary>
    public string? Application { get; set; }
    public string? Producer { get; set; }

    /// <summary>
    /// When the document was first written, which is usually long before it was uploaded.
    /// A true instant when <see cref="AuthoredOffsetMinutes"/> is set. Without an offset this
    /// holds the author's wall clock, so it is right for display but not an exact instant.
    /// </summary>
    public DateTime? AuthoredAt { get; set; }
    public short? AuthoredOffsetMinutes { get; set; }

    /// <inheritdoc cref="AuthoredAt"/>
    public DateTime? ModifiedAt { get; set; }
    public short? ModifiedOffsetMinutes { get; set; }

    /// <summary>
    /// Counts as the authoring app recorded them. Word processors only update them on save,
    /// so they can lag behind the content.
    /// </summary>
    public int? PageCount { get; set; }
    public int? WordCount { get; set; }
    public int? SlideCount { get; set; }
    public int? SheetCount { get; set; }

    /// <summary>
    /// Total time the document was open for editing, as the authoring app tracked it.
    /// </summary>
    public int? EditingMinutes { get; set; }

    /// <summary>
    /// The PDF's first page size by name, like A4 or Letter. Null when it matches no standard size.
    /// </summary>
    public string? PaperSize { get; set; }

    /// <summary>
    /// Book fields, which only EPUB files carry. A book's author is public information, unlike
    /// the account names office files record.
    /// </summary>
    public string? Publisher { get; set; }

    /// <summary>
    /// Normalized to the 13-digit form, so the same book matches whichever form its file used.
    /// </summary>
    public string? Isbn { get; set; }

    public string? Series { get; set; }

    /// <summary>
    /// The book's place in <see cref="Series"/>. Fractional because series slot novellas in
    /// between, as in book 2.5.
    /// </summary>
    public decimal? SeriesIndex { get; set; }

    /// <summary>
    /// True when a store's copy protection encrypts the book's pages. Its properties stay
    /// readable, unlike a password-protected document.
    /// </summary>
    public bool IsDrmProtected { get; set; }

    /// <summary>
    /// True when the file needs a password to open, so nothing beyond this flag could be read.
    /// </summary>
    public bool IsPasswordProtected { get; set; }

    /// <summary>
    /// Every field the readers found, so a new column can be filled without reading every
    /// original again. Holds names, template paths and printer names, so it is never returned
    /// by the API.
    /// </summary>
    public required string RawJson { get; set; }

    /// <summary>
    /// Which extraction logic wrote this row. Raising the current version makes the backfill
    /// process the row again.
    /// </summary>
    public int ExtractorVersion { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
