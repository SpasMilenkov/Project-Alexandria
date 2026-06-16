using Alexandria.Common.Exceptions.FileVersions;
using Alexandria.Common.Exceptions.SignedUrls;
using Alexandria.Dto.SignedUrls;

namespace Alexandria.Common.Services;

/// <summary>Provides operations for generating and consuming file share links.</summary>
public interface ISignedUrlService
{
    /// <summary>
    /// Creates a new share link for a file.
    /// When <paramref name="fileVersionId"/> is provided the link permanently targets that version;
    /// otherwise it always resolves to the file's current version.
    /// Throws <see cref="FileNotFoundException"/> if the file is not owned by the user.
    /// Throws <see cref="FileVersionNotFoundException"/> if the version does not belong to the file.
    /// </summary>
    Task<CreateShareLinkResponse> CreateShareLinkAsync(
        Guid fileId,
        Guid userId,
        TimeSpan? expiry,
        Guid? fileVersionId = null,
        int? maxAccessCount = null,
        CancellationToken ct = default);

    /// <summary>
    /// Returns public metadata for a shared file.
    /// Throws <see cref="SignedUrlNotFoundException"/> or
    /// <see cref="SignedUrlExpiredException"/> on invalid/expired tokens.
    /// </summary>
    Task<SharedFileMetadataDto> GetSharedFileMetadataAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Generates a short-lived presigned S3 download URL for the file or pinned version.
    /// Throws <see cref="SignedUrlNotFoundException"/> or
    /// <see cref="SignedUrlExpiredException"/> on invalid/expired tokens.
    /// </summary>
    Task<ShareDownloadResponse> GetDownloadUrlAsync(string token, CancellationToken ct = default);

    ///TODO: Change it to paginated result instead of raw list
    /// 
    /// <summary>Returns all active share links for a file. Only the file owner may call this.</summary>
    Task<IEnumerable<ShareLinkSummaryDto>> GetShareLinksForFileAsync(
        Guid fileId,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>Revokes a share link. Returns false if not found or not owned by the user.</summary>
    Task<bool> RevokeShareLinkAsync(Guid id, Guid userId, CancellationToken ct = default);
}