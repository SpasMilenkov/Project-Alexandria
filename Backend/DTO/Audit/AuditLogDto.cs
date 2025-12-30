using Models.Enumerators;

namespace DTO;

public record AuditLogDto(OperationType OperationType,
    EntityType EntityType,
    Guid UserId,
    Guid EntityId,
    string Description,
    string? MetadataJson = null,
    string IpAddress = "");