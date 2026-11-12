using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Audit;

public sealed class AuditEntry
{
    public required AuditEventCode EventCode { get; init; }
    public required OperationType OperationType { get; init; }
    public required EntityType EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public Guid? OwnerId { get; init; }
    public Guid? ParentEntityId { get; init; }
    public string? EntityName { get; init; }

    /// <summary>Snake_case keys, matching the rows triggers write.</summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }

    /// <summary>
    /// Overrides the request's actor, for events that happen before anyone is signed in, such as
    /// a failed login attributed to the account it targeted.
    /// </summary>
    public Guid? ActorId { get; init; }
}
