using Alexandria.Data.Models.Enumerators.Monitoring;

namespace Alexandria.Dto.Events.Operational;

public sealed record EventCalendarRow(DateTime CreatedAt, ServiceType ServiceType, OperationalEventSeverity Severity);
