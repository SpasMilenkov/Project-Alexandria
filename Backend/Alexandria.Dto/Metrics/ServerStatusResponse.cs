namespace Alexandria.Dto.Metrics;

public record ServerStatusResponse(
    string Status,
    DateTimeOffset CheckedAt,
    double Duration,
    HealthSummary Summary,
    IEnumerable<HealthCheckEntry> Checks
);