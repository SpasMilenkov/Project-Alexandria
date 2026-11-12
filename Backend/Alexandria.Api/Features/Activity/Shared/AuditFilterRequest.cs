using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Api.Features.Activity.Shared;

/// <summary>Filters shared by the personal export and the admin explorer.</summary>
public class AuditFilterRequest
{
    public EntityType? EntityType { get; set; }
    public Guid? EntityId { get; set; }

    /// <summary>Repeatable: <c>?eventCodes=1&amp;eventCodes=4</c>.</summary>
    public AuditEventCode[]? EventCodes { get; set; }

    public OperationType? OperationType { get; set; }
    public LogSource? Source { get; set; }
    public string? IpAddress { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public Guid? CorrelationId { get; set; }
}
