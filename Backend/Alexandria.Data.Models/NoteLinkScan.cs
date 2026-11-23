namespace Alexandria.Data.Models;

/// <summary>
/// Marks stored text as read for <c>alexandria://</c> links, with the links found in it. It hangs
/// off the content rather than a file or version: the links are a property of the text alone, so
/// a restored version or an identical upload reuses the scan, and whether the text counts as a
/// note (where its file lives) is decided when the links are read.
/// </summary>
public class NoteLinkScan : IBase
{
    public Guid ContentObjectId { get; set; }
    public ContentObject? ContentObject { get; set; }

    /// <summary>The extractor version that read the text, so a smarter one can read it again.</summary>
    public int ScannerVersion { get; set; }

    public ICollection<NoteLink> Links { get; set; } = new List<NoteLink>();

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
