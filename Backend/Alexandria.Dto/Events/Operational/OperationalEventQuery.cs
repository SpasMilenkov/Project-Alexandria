using Alexandria.Data.Models.Enumerators.Monitoring;

namespace Alexandria.Dto.Events.Operational;

public record OperationalEventQuery(
    ServiceType? ServiceType,
    OperationalEventStatus? Status,
    OperationalEventSeverity? Severity,
    OperationalEventCode? Code,
    DateTime? From,
    DateTime? To,
    int Page,
    int PageSize);