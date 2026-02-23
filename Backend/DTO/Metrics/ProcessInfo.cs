namespace DTO.Metrics;

public record ProcessInfo(
    double CpuTimeSeconds,
    long WorkingSetMb,
    long GcTotalMemoryMb,
    long MemoryLimitMb,
    double MemoryUsagePercent,
    int ThreadCount,
    TimeSpan Uptime,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections
);
