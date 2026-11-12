using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Audit;

public sealed class AuditPageRequest
{
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public AuditSortBy SortBy { get; init; } = AuditSortBy.Timestamp;
    public SortDirection SortDirection { get; init; } = SortDirection.Desc;
}
