using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Audit;

public record AuditLogDto(
    OperationType OperationType,
    EntityType EntityType,
    Guid UserId,
    Guid EntityId,
    AuditEventCode EventCode,
    string? MetadataJson = null,
    string IpAddress = "");