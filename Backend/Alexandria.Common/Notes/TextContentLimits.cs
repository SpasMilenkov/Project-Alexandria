namespace Alexandria.Common.Notes;

/// <summary>Limits for text the editor reads and writes directly, rather than through uploads.</summary>
public static class TextContentLimits
{
    // Matches the editor's own limit: past this size the editor gets sluggish on every keystroke,
    // and the whole text travels in one request body.
    public const int MaxBytes = 1_000_000;
}
