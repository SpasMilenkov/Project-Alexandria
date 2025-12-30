using Models.Enumerators;

namespace DTO.Audit;

public sealed class AuditLogResult
{
    public OperationType OperationType { get; set; }
    public EntityType EntityType { get; set; }
    public Guid UserId { get; set; }
    public Guid EntityId { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public LogSource LogSource { get; set; }
}