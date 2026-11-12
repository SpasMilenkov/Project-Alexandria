using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Audit;

/// <summary>
/// The current user's own history. Rows are always scoped to the caller, so the query carries no
/// user id.
/// </summary>
public sealed class AuditLogQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public AuditSortBy SortBy { get; set; } = AuditSortBy.Timestamp;
    public SortDirection SortDirection { get; set; } = SortDirection.Desc;
    public Guid? EntityId { get; set; }
    public OperationType? OperationType { get; set; }
    public EntityType? EntityType { get; set; }
    public LogSource? Source { get; set; }
    public string? IpAddress { get; set; }
    public DateTimeOffset? Before { get; set; }
    public DateTimeOffset? After { get; set; }
}
