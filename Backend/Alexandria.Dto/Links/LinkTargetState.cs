namespace Alexandria.Dto.Links;

/// <summary>How a link target looks to the person reading the note.</summary>
public enum LinkTargetState
{
    Available = 0,

    /// <summary>In the viewer's trash, so it can still be restored. Only files and folders have one.</summary>
    Trashed = 1,

    /// <summary>
    /// Missing, deleted for good, or someone else's. The three are reported the same way so a link
    /// never reveals whether another account's item exists.
    /// </summary>
    Unavailable = 2,
}
