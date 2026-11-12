using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using Alexandria.Dto.Files;

namespace Alexandria.Common.Repositories;

// AuditLog rows are append-only and carry no IBase timestamps, so the generic IRepository
// contract (soft delete, Update) does not apply here.
public interface IAuditLogRepository
{
    Task<PaginatedResult<AuditLogResult>> FetchAuditLogAsync(
        AuditLogFilter filter,
        AuditPageRequest page,
        CancellationToken ct = default);

    /// <summary>Newest first, at most <paramref name="maxRows"/> rows, streamed for exports.</summary>
    IAsyncEnumerable<AuditLogResult> StreamAsync(AuditLogFilter filter, int maxRows, CancellationToken ct = default);

    /// <summary>
    /// The newest <paramref name="limit"/> matching rows, returned in <paramref name="order"/>. The
    /// order never changes which rows are picked, only how they come back.
    /// </summary>
    Task<IReadOnlyList<AuditLogResult>> GetRecentAsync(
        AuditLogFilter filter,
        int limit,
        SortDirection order = SortDirection.Desc,
        CancellationToken ct = default);

    /// <summary>Deletes every row older than <paramref name="cutoff"/> and returns how many went.</summary>
    Task<int> DeleteRangeAsync(DateTimeOffset cutoff, CancellationToken ct = default);

    Task<bool> AnyAsync(AuditLogFilter filter, CancellationToken ct = default);

    Task<int> CountAsync(AuditLogFilter filter, CancellationToken ct = default);

    /// <summary>
    /// Positions of matching rows in timestamp order, without loading the rows themselves, so an
    /// export can be split at exact row boundaries.
    /// </summary>
    Task<IReadOnlyList<AuditCursor>> GetPositionsAsync(
        AuditLogFilter filter,
        int skip,
        int take,
        SortDirection order = SortDirection.Desc,
        CancellationToken ct = default);

    Task<AuditLogResult?> GetLatestAsync(AuditEventCode eventCode, CancellationToken ct = default);

    /// <summary>
    /// Rows of <paramref name="eventCode"/> from <paramref name="from"/> on, reduced to the columns
    /// the access overview reads. Metadata only comes back when asked for.
    /// </summary>
    Task<IReadOnlyList<AuditSignInRow>> GetSignInRowsAsync(
        AuditEventCode eventCode,
        DateTimeOffset from,
        bool includeMetadata,
        CancellationToken ct = default);

    /// <summary>
    /// One row per account and address with its first and last sign-in and how many there were,
    /// so history is summarised in the database instead of loaded row by row. Narrowed to one
    /// account and a start date when given.
    /// </summary>
    Task<IReadOnlyList<AuditSignInHistoryRow>> GetSignInHistoryAsync(
        Guid? ownerId = null,
        DateTimeOffset? from = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<AuditActivityRow>> GetActivityRowsAsync(
        Guid userId,
        ActivityQuery query,
        CancellationToken ct = default);
}
