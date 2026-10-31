namespace Alexandria.Common.Services;

/// <summary>
/// Owns user-facing static bytes (background images today, profile pictures tomorrow).
/// Bucket-aligned split of the former monolithic storage service; behavior preserved verbatim.
/// </summary>
public interface IStaticAssetService
{
    /// <summary>
    /// Validates (size cap, server-side image sniffing) and stores a background image
    /// under a fresh server-derived per-owner key. Returns the key.
    /// </summary>
    /// <exception cref="InvalidOperationException">Over cap or unsupported image content.</exception>
    Task<string> UploadBackgroundImageAsync(
        Stream content,
        Guid userId,
        CancellationToken ct = default);

    Task DeleteBackgroundImageAsync(string objectKey, CancellationToken ct = default);

    /// <summary>
    /// Validates (ownership, size cap, server-side image sniffing) and stores a playlist
    /// cover under its stable gate-mapped key. Returns the key.
    /// </summary>
    /// <exception cref="InvalidOperationException">Over cap or unsupported image content.</exception>
    Task<string> UploadPlaylistCoverAsync(
        Stream content,
        Guid playlistId,
        Guid userId,
        CancellationToken ct = default);
}
