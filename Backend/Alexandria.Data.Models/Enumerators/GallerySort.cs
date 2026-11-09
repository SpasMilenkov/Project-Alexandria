namespace Alexandria.Data.Models.Enumerators;

/// <summary>
/// Which date orders the photo timeline. Both run newest first.
/// </summary>
public enum GallerySort
{
    /// <summary>Capture date, falling back to the upload date for photos without one.</summary>
    Taken = 0,

    /// <summary>When the file was uploaded.</summary>
    Uploaded = 1,
}