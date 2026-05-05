namespace Alexandria.Dto.Metrics;

public record HealthSummary(int Healthy, int Degraded, int Unhealthy);