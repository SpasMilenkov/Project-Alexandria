using System.Text.Encodings.Web;
using System.Text.Json;
using Alexandria.Common.Audit;
using Alexandria.Common.Config;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;

namespace Alexandria.Data.Audit;

public sealed class AuditWriter(AlexandriaDbContext context, IAuditActorAccessor actor) : IAuditWriter
{
    // Keys arrive in snake_case already; the options only need to keep values compact and stable.
    internal static readonly JsonSerializerOptions MetadataJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    public void Record(AuditEntry entry)
    {
        var actorId = entry.ActorId ?? actor.UserId ?? SystemConfig.SystemId;
        var name = entry.EntityName;
        var maxName = ValidationConstants.StringLengths.LongString;

        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.CreateVersion7(),
            OperationType = entry.OperationType,
            UserId = actorId,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            EventCode = entry.EventCode,
            MetadataJson = entry.Metadata is null
                ? null
                : JsonSerializer.Serialize(entry.Metadata, MetadataJsonOptions),
            Timestamp = DateTimeOffset.UtcNow,
            IpAddress = actor.IpAddress,
            Source = actorId == SystemConfig.SystemId ? LogSource.System : LogSource.API,
            OwnerId = entry.OwnerId,
            ParentEntityId = entry.ParentEntityId,
            CorrelationId = actor.CorrelationId,
            EntityName = name is not null && name.Length > maxName ? name[..maxName] : name,
        });
    }
}
