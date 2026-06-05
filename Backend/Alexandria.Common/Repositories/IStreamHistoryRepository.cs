using Alexandria.Data.Models;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;

namespace Alexandria.Common.Repositories;

/// <summary>
/// Repository for <see cref="StreamHistory"/> and <see cref="StreamSession"/> persistence.
/// </summary>
public interface IStreamHistoryRepository : IRepository<StreamHistory>
{
    /// <summary>Creates and persists a new stream history row, returning the saved instance.</summary>
    Task<StreamHistory> CreateAsync(StreamHistory entity, CancellationToken ct = default);

    /// <summary>Persists changes to an existing stream history row and returns the updated instance.</summary>
    Task<StreamHistory> UpdateAsync(StreamHistory entity, CancellationToken ct = default);

    /// <summary>
    /// Returns the stream history for a specific user and file, or <c>null</c> if none exists.
    /// </summary>
    Task<StreamHistory?> GetByUserAndFileAsync(Guid userId, Guid fileId, CancellationToken ct = default);

    /// <summary>
    /// Returns the stream history row with the given id, scoped to the given user.
    /// Returns <c>null</c> when not found or the row belongs to a different user.
    /// </summary>
    Task<StreamHistory?> GetByIdAndUserIdAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>Returns a paginated list of stream history rows for a user, applying optional filters.</summary>
    Task<PaginatedResult<StreamHistoryDto>> FindAsync(
        Guid userId,
        StreamHistoryQuery query,
        CancellationToken ct = default);

    /// <summary>Creates and persists a new session row, returning the saved instance.</summary>
    Task<StreamSession> CreateSessionAsync(StreamSession session, CancellationToken ct = default);

    /// <summary>Persists changes to an existing session row and returns the updated instance.</summary>
    Task<StreamSession> UpdateSessionAsync(StreamSession session, CancellationToken ct = default);

    /// <summary>
    /// Returns the session with the given id, or <c>null</c> if not found.
    /// </summary>
    Task<StreamSession?> GetSessionByIdAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>Returns all sessions for a given stream history row, ordered by start time ascending.</summary>
    Task<PaginatedResult<StreamSessionDto>> GetSessionsAsync(Guid streamHistoryId, int page = 1, int pageSize = 25,
        CancellationToken ct = default);
}