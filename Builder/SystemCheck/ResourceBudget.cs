using System.Globalization;
using Builder.Catalog;

namespace Builder.SystemCheck;

// Fits every container's memory and CPU cap to the machine, so that no mix of jobs can
// starve the host. Memory caps must add up to what the machine can spare, since an
// overcommitted cap set lets the host run out before any container reaches its own cap.
// CPU caps only throttle, so they may overlap, but the long CPU-bound jobs together must
// still leave the host's reserved cores free.
public static class ResourceBudget
{
    public const string EnvironmentPrefix = "LIMIT_";
    public const long MinimumHostReserveMb = 2048;
    public const long MaximumHostReserveMb = 4096;
    public const double HostReserveShare = 0.15;

    private const long MemoryStepMb = 64;

    public static long HostReserveMb(long machineMb) =>
        Math.Clamp((long)(machineMb * HostReserveShare), MinimumHostReserveMb, MaximumHostReserveMb);

    public static long UsableMemoryMb(long machineMb) => Math.Max(0, machineMb - HostReserveMb(machineMb));

    public static int ReservedCpuCores(int cores)
    {
        if (cores >= 8) return 2;
        if (cores >= 2) return 1;

        return 0;
    }

    public static int UsableCpuCores(int cores) => Math.Max(1, cores - ReservedCpuCores(cores));

    public static IReadOnlyList<ServiceAllocation> Plan(ResolvedCapabilities capabilities, SystemResources resources)
    {
        var services = capabilities.Services.Where(service => !service.OneShot).ToArray();
        var memory = PlanMemory(services, ResourceCalculator.MemoryLimitMb(resources));
        var cpus = PlanCpus(services, ResourceCalculator.CpuLimit(resources), resources.VideoGpus.Count > 0);

        return services.Select(service => new ServiceAllocation
        {
            ServiceId = service.Id,
            MemoryMb = memory[service.Id],
            Cpus = cpus[service.Id],
        }).ToArray();
    }

    public static Dictionary<string, string> EnvironmentValues(IEnumerable<ServiceAllocation> allocations)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var allocation in allocations)
        {
            values[MemoryKey(allocation.ServiceId)] = FormatMemory(allocation.MemoryMb);
            values[CpuKey(allocation.ServiceId)] = FormatCpus(allocation.Cpus);
        }

        return values;
    }

    // Replaces every limit in an installation's environment, dropping those of services no
    // longer selected, so the caps always match the current machine and selection.
    public static void ApplyEnvironment(Dictionary<string, string> values, ResolvedCapabilities capabilities, SystemResources resources)
    {
        var limits = EnvironmentValues(Plan(capabilities, resources));

        foreach (var key in values.Keys.Where(key => key.StartsWith(EnvironmentPrefix, StringComparison.Ordinal) && !limits.ContainsKey(key)).ToArray())
        {
            values.Remove(key);
        }

        foreach (var (key, value) in limits)
        {
            values[key] = value;
        }
    }

    public static string MemoryKey(string serviceId) => EnvironmentPrefix + KeyName(serviceId) + "_MEMORY";

    public static string CpuKey(string serviceId) => EnvironmentPrefix + KeyName(serviceId) + "_CPUS";

    public static string FormatMemory(long memoryMb) => memoryMb.ToString(CultureInfo.InvariantCulture) + "M";

    public static string FormatCpus(double cpus) => cpus.ToString("0.##", CultureInfo.InvariantCulture);

    private static Dictionary<string, long> PlanMemory(IReadOnlyList<ServiceInfo> services, long machineMb)
    {
        var caps = services.ToDictionary(service => service.Id, service => service.MemoryMb, StringComparer.Ordinal);

        // Without a measured machine there is nothing to fit against, so the catalog caps stand.
        if (machineMb <= 0) return caps;

        var usableMb = UsableMemoryMb(machineMb);
        var totalMb = caps.Values.Sum();

        if (totalMb <= usableMb) return caps;

        var flexible = services.Where(IsShrinkable).ToArray();
        var fixedMb = totalMb - flexible.Sum(service => service.MemoryMb);
        var floorMb = flexible.Sum(service => service.MinimumMemoryMb);
        var spareMb = flexible.Sum(service => service.MemoryMb - service.MinimumMemoryMb);
        var share = spareMb == 0 ? 0 : Math.Clamp((double)(usableMb - fixedMb - floorMb) / spareMb, 0, 1);

        foreach (var service in flexible)
        {
            var shrunkMb = service.MinimumMemoryMb + (long)((service.MemoryMb - service.MinimumMemoryMb) * share);
            caps[service.Id] = Math.Max(service.MinimumMemoryMb, shrunkMb / MemoryStepMb * MemoryStepMb);
        }

        return caps;
    }

    private static Dictionary<string, double> PlanCpus(IReadOnlyList<ServiceInfo> services, int cores, bool videoOnGpu)
    {
        if (cores <= 0)
            return services.ToDictionary(service => service.Id, service => service.Cpus, StringComparer.Ordinal);

        var usable = UsableCpuCores(cores);
        var cpus = services.ToDictionary(service => service.Id, service => Math.Min(CpuCap(service, videoOnGpu), usable),
            StringComparer.Ordinal);
        var heavy = services.Where(service => service.HeavyCpu).ToArray();
        var heavyTotal = heavy.Sum(service => cpus[service.Id]);

        if (heavyTotal <= usable) return cpus;

        // Whole cores keep the thread counts handed to ffmpeg and TensorFlow exact.
        var share = usable / heavyTotal;

        foreach (var service in heavy)
        {
            cpus[service.Id] = Math.Max(1, Math.Floor(cpus[service.Id] * share));
        }

        return cpus;
    }

    // A job on the GPU needs fewer cores, which leaves the other heavy jobs a larger share; a
    // fallback to the CPU encoder then runs slower instead of past the host's reserve.
    private static double CpuCap(ServiceInfo service, bool videoOnGpu) =>
        videoOnGpu && service.GpuCpus > 0 ? service.GpuCpus : service.Cpus;

    private static bool IsShrinkable(ServiceInfo service) =>
        service.MinimumMemoryMb > 0 && service.MinimumMemoryMb < service.MemoryMb;

    private static string KeyName(string serviceId) => serviceId.ToUpperInvariant().Replace('-', '_');
}
