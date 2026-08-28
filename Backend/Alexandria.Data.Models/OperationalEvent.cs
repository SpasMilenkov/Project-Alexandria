using Alexandria.Data.Models.Enumerators.Monitoring;

namespace Alexandria.Data.Models;

public class OperationalEvent : IBase
{
    public Guid Id { get; set; }

    public ServiceType ServiceType { get; set; }
    public OperationalEventStatus Status { get; set; }
    public OperationalEventCode Code { get; set; }
    public OperationalEventSeverity Severity { get; set; }
    public string? MetadataJson { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public required Guid CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}