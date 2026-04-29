using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Audit;

public sealed class AuditLogResult
{
    public required OperationType OperationType { get; set; }
    public required EntityType EntityType { get; set; }
    public required Guid UserId { get; set; }
    public required Guid EntityId { get; set; }
    public required AuditEventCode EventCode { get; set; }
    public string? FallbackDescription { get; set; }
    public string? IpAddress { get; set; }
    public string? Metadata { get; set; }
    public required DateTimeOffset Timestamp { get; set; }
    public required LogSource LogSource { get; set; }
}