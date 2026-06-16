using Alexandria.Data.Models;

namespace Alexandria.Common.Repositories;

/// <summary>Repository for <see cref="SignedUrl"/> persistence operations.</summary>
public interface ISignedUrlRepository : IRepository<SignedUrl>
{
    /// <summary>
    /// Finds a non-deleted signed URL by its token, including the related file info.
    /// Returns null if no match or already soft-deleted.
    /// </summary>
    Task<SignedUrl?> GetByTokenAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Returns all non-deleted signed URLs for the given file, ordered newest first.
    /// </summary>
    Task<IEnumerable<SignedUrl>> GetByFileIdAsync(Guid fileId, CancellationToken ct = default);

    /// <summary>Creates and persists a new signed URL, returning the saved instance.</summary>
    Task<SignedUrl> CreateAsync(SignedUrl entity, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a signed URL by id, verifying it belongs to the requesting user.
    /// Returns false if not found or not owned by the user.
    /// </summary>
    Task<bool> RevokeAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Increments the access counter on a given signed URL link
    /// </summary>
    /// <param name="id">The id of the URL</param>
    /// <param name="ct">Cancellation token</param>
    Task IncrementAccessCountAsync(Guid id, CancellationToken ct = default);
}