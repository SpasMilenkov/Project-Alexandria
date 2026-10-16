namespace Alexandria.Common.Services;

/// <summary>
/// Owns user-facing static bytes (background images today, profile pictures tomorrow).
/// Bucket-aligned split of the former monolithic storage service; behavior preserved verbatim.
/// </summary>
public interface IStaticAssetService
{
    Task<string> GenerateImageUploadUrl(string objectKey, TimeSpan expiry);

    Task<string> GenerateBackgroundImageGetUrl(string objectKey, TimeSpan expiry);

    Task DeleteBackgroundImageAsync(string objectKey, CancellationToken ct = default);

    Task<string> GetPlaylistCoverUploadUrlAsync(
        Guid playlistId,
        Guid userId,
        string contentType,
        CancellationToken ct = default);
}