using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Data.Models;

public class AuditLog
{
    public Guid Id { get; set; }
    public required OperationType OperationType { get; set; }
    public Guid? UserId { get; set; }
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
    public bool IsEnriched { get; set; }
}