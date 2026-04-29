using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto;

public sealed class AuditLogQuery
{
    public int Page { get; set; }

    public int PageSize { get; set; }

    //TODO: Convert this to a proper enum
    public string SortBy { get; set; } = "timestamp";
    public SortDirection SortDirection { get; set; }
    public Guid UserId { get; set; }
    public Guid? EntityId { get; set; }
    public OperationType? OperationType { get; set; }
    public EntityType? EntityType { get; set; }
    public string? IpAddress { get; set; }
    public DateTime? Before { get; set; }
    public DateTime? After { get; set; }
}