using Alexandria.Data.Models.Enumerators.Monitoring;

namespace Alexandria.Dto.Status;

// Trimmed three-state vocabulary for the anonymous-facing status page — never
// leak codes, metadata or actors through this surface.
public static class PublicServiceState
{
    public const string Healthy = "Healthy";
    public const string Degraded = "Degraded";
    public const string Down = "Down";
}

public sealed record DailyStatusPoint(DateOnly Date, string Status);

public sealed record DailyServiceStatus(
    ServiceType ServiceType,
    IReadOnlyList<DailyStatusPoint> Days);