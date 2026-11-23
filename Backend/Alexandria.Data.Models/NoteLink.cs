using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Data.Models;

/// <summary>
/// One distinct link target found in a note's text. The target may not exist (deleted, or never
/// readable by the note's owner); the row is kept anyway so a restored item gets its mentions back,
/// and access is checked when the links are read.
/// </summary>
public class NoteLink
{
    public Guid ContentObjectId { get; set; }
    public NoteLinkScan? Scan { get; set; }

    public LinkTargetKind TargetKind { get; set; }
    public Guid TargetId { get; set; }

    /// <summary>The line around the first link to the target, as markdown, for "Mentioned in".</summary>
    public string Snippet { get; set; } = string.Empty;
}
