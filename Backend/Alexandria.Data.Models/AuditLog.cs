using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Data.Models;

public class AuditLog
{
    public Guid Id { get; set; }
    public required OperationType OperationType { get; set; }
    public Guid UserId { get; set; }
    public required EntityType EntityType { get; set; }
    public Guid? EntityId { get; set; }

    // Replaces hardcoded Description — frontend resolves this to a string
    public required AuditEventCode EventCode { get; set; }

    public string? MetadataJson { get; set; }

    // Kept for trigger-originated logs that came in before enrichment,
    // or as a human-readable fallback for external DB changes
    public string? FallbackDescription { get; set; }

    public DateTimeOffset Timestamp { get; set; }
    public string? IpAddress { get; set; }
    public LogSource Source { get; set; }

    // Whose data was affected; differs from UserId when an admin or the system acts on a user's data
    public Guid? OwnerId { get; set; }

    // Owning file for versions, previews, file tags and share links, so one query returns a file's history
    public Guid? ParentEntityId { get; set; }

    // Shared by every row written in one request or worker message
    public Guid? CorrelationId { get; set; }

    // Name snapshot so rows stay readable after the entity is deleted
    public string? EntityName { get; set; }
}