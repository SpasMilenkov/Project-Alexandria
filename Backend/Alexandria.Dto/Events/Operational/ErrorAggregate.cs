using Alexandria.Data.Models.Enumerators.Monitoring;

namespace Alexandria.Dto.Events.Operational;

public record ErrorAggregate(
    DateOnly Day,
    ServiceType ServiceType,
    OperationalEventSeverity Severity,
    int Count);