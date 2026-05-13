using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;

namespace Alexandria.Common.Services;

public interface IStreamHistoryService
{
    /// <summary>
    /// Returns the stream history entry for the given user and file combination,
    /// or <see langword="null"/> when no history has been recorded yet.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="fileId">The file identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<StreamHistoryResponse?> GetByUserAndFileAsync(
        Guid userId,
        Guid fileId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns a paginated list of stream history entries matching the given query filters.
    /// </summary>
    /// <param name="query">Filtering and pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<PaginatedResult<StreamHistoryResponse>> FindHistoryAsync(
        StreamHistoryQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Creates or updates the playback position for the given user and file.
    /// Idempotent — safe to call on every progress tick.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="fileId">The file identifier.</param>
    /// <param name="positionSeconds">The current playback position in seconds.</param>
    /// <param name="completed">
    /// <see langword="true"/> when the file has been played to completion.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task UpsertPositionAsync(
        Guid userId,
        Guid fileId,
        long positionSeconds,
        bool completed,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the most recently accessed stream history entries for the given user,
    /// ordered by <c>LastAccessedAt</c> descending.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="count">
    /// Maximum number of entries to return. Must be greater than zero.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="count"/> is less than or equal to zero.
    /// </exception>
    Task<IEnumerable<StreamHistoryResponse>> GetRecentByUserAsync(
        Guid userId,
        int count,
        CancellationToken ct = default);
}