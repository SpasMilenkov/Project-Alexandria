using Alexandria.Common.Exceptions.Streaming;
using Alexandria.Data.Models;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;

namespace Alexandria.Common.Services;

/// <summary>Provides operations for tracking and querying a user's streaming history.</summary>
public interface IStreamHistoryService
{
    /// <summary>
    /// Returns the stream history for the given file and user, or <c>null</c> if the file has
    /// never been played by this user.
    /// </summary>
    Task<StreamHistoryDto?> GetByFileAsync(Guid fileId, Guid userId, CancellationToken ct = default);

    /// <summary>Returns a paginated list of stream history records for the given user.</summary>
    Task<PaginatedResult<StreamHistoryDto>> FindAsync(Guid userId,
        StreamHistoryQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all sessions for a stream history row. Throws <see cref="StreamHistoryNotFoundException"/>
    /// if the row does not belong to the user.
    /// </summary>
    Task<PaginatedResult<StreamSessionDto>> GetSessionsAsync(Guid streamHistoryId, Guid userId, int page = 1,
        int pageSize = 25,
        CancellationToken ct = default);

    /// <summary>
    /// Opens a new playback session. Creates the parent stream history row if this is the
    /// first time the user plays this file.
    /// </summary>
    Task<StreamSessionDto> StartSessionAsync(StartSessionRequest request, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Closes an open session, recording the end position and listened seconds. Updates the
    /// parent <see cref="StreamHistory"/> summary fields (position, max reached, total listened,
    /// completion count). Throws <see cref="StreamSessionNotFoundException"/> or
    /// <see cref="StreamSessionAlreadyClosedException"/> when appropriate.
    /// </summary>
    Task<StreamHistoryDto> CloseSessionAsync(Guid sessionId, CloseSessionRequest request, Guid userId,
        CancellationToken ct = default);
}