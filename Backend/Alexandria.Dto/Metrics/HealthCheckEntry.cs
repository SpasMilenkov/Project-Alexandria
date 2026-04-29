namespace Alexandria.Dto.Metrics;

public record HealthCheckEntry(
    string Name,
    string Status,
    string? Description,
    double Duration,
    IEnumerable<string> Tags,
    string? Error,
    IReadOnlyDictionary<string, object> Data
);