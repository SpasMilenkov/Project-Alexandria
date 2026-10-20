namespace Alexandria.Data.Models;

public class PlaylistItem : IBase
{
    public Guid Id { get; set; }
    public int Position { get; set; }

    public Guid PlaylistId { get; set; }
    public Playlist? Playlist { get; set; }

    public Guid TranspilationJobId { get; set; }
    public TranspilationJob? TranspilationJob { get; set; }

    /// <summary>
    /// True when the user removed this item through the normal playlist UI. The
    /// reconciler never re-adds such rows (it skips instead of undeleting), while
    /// system removals (plain soft-delete) reappear automatically on re-match.
    /// Manually re-adding a track clears this flag.
    /// </summary>
    public bool RemovedByUser { get; set; } = false;

    /// <summary>
    /// True when the user placed this item on the playlist, either by adding it
    /// directly or by restoring it after removal. Sync never sets this flag; it
    /// keeps pinned live rows even when the track no longer matches the grouping.
    /// </summary>
    public bool AddedByUser { get; set; } = false;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}