using DTO.Metrics;
using FastEndpoints;

namespace API.Features.Monitoring.GetResourceUsage;

sealed class GetServerResourcesEndpoint : EndpointWithoutRequest<ServerResourcesResponse>
{
    private static readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;

    public override void Configure()
    {
        Get("monitoring/resources");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(10);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var proc = System.Diagnostics.Process.GetCurrentProcess();
        var gcInfo = GC.GetGCMemoryInfo();

        var totalMemoryBytes = gcInfo.TotalAvailableMemoryBytes;
        var usedMemoryBytes = proc.WorkingSet64;

        var response = new ServerResourcesResponse(
            Process: new ProcessInfo(
                CpuTimeSeconds: Math.Round(proc.TotalProcessorTime.TotalSeconds, 2),
                WorkingSetMb: proc.WorkingSet64 / 1024 / 1024,
                GcTotalMemoryMb: GC.GetTotalMemory(forceFullCollection: false) / 1024 / 1024,
                MemoryLimitMb: totalMemoryBytes / 1024 / 1024,
                MemoryUsagePercent: Math.Round((double)usedMemoryBytes / totalMemoryBytes * 100, 2),
                ThreadCount: proc.Threads.Count,
                Uptime: DateTimeOffset.UtcNow - _startedAt,
                Gen0Collections: GC.CollectionCount(0),
                Gen1Collections: GC.CollectionCount(1),
                Gen2Collections: GC.CollectionCount(2)
            ),
            CheckedAt: DateTimeOffset.UtcNow
        );

        await Send.OkAsync(response, cancellation: ct);
    }
}
