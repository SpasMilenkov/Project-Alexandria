using Models.Enumerators;

namespace DTO.Audit;

public record AuditLogDto(OperationType OperationType,
    EntityType EntityType,
    Guid UserId,
    Guid EntityId,
    AuditEventCode EventCode,
    string? MetadataJson = null,
    string IpAddress = "");