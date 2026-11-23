namespace Alexandria.Data.Models;

/// <summary>
/// The editor's working text for a file, kept between versions. Autosave writes here, so typing
/// never makes a version; a checkpoint turns the draft into the next version and removes it. A
/// file has at most one draft, and only its owner edits it.
/// </summary>
public class FileDraft : IBase
{
    public Guid FileId { get; set; }
    public File? File { get; set; }

    /// <summary>
    /// The version the draft was made from. Once the file has moved past it, the draft can no
    /// longer be written or checkpointed without someone deciding which text wins.
    /// </summary>
    public Guid BaseVersionId { get; set; }

    /// <summary>
    /// Changes on every write. An editor names the revision it last saw, so two tabs on the same
    /// version can't overwrite each other's drafts unnoticed.
    /// </summary>
    public Guid Revision { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
