using Models;
using Models.Enumerators;

namespace DTO.Extensions;

public static class AuditLogExtensions
{
    public static AuditLog ToEntity(this AuditLogDto dto)
    {
        return new AuditLog
        {
            OperationType = dto.OperationType,
            UserId = dto.UserId,
            EntityType = dto.EntityType,
            EntityId = dto.EntityId,
            Description = dto.Description,
            MetadataJson = dto.MetadataJson,
            Timestamp = DateTimeOffset.UtcNow,
            IpAddress = dto.IpAddress,
            Source = LogSource.API,
        };
    }
}