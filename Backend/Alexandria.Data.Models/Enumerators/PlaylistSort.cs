namespace Alexandria.Data.Models.Enumerators;

/// <summary>
/// Server-side ordering for the playlist list. Applied in the repository before
/// paging so page contents and totals stay consistent with browsing filters.
/// </summary>
public enum PlaylistSort
{
    Newest = 0,
    Name = 1,
    TrackCount = 2,
}