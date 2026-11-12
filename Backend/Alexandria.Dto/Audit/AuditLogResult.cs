using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Audit;

public sealed class AuditLogResult
{
    public required Guid Id { get; set; }
    public required OperationType OperationType { get; set; }
    public required EntityType EntityType { get; set; }
    public required Guid UserId { get; set; }

    /// <summary>Display name of the actor; null for the system user or a deleted account.</summary>
    public string? UserName { get; set; }

    public required Guid EntityId { get; set; }
    public Guid? OwnerId { get; set; }
    public Guid? ParentEntityId { get; set; }
    public Guid? CorrelationId { get; set; }
    public string? EntityName { get; set; }
    public required AuditEventCode EventCode { get; set; }
    public string? FallbackDescription { get; set; }
    public string? IpAddress { get; set; }
    public string? Metadata { get; set; }
    public required DateTimeOffset Timestamp { get; set; }
    public required LogSource LogSource { get; set; }
}
