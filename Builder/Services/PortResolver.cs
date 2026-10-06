using System.Net.NetworkInformation;
using Builder.Models;

namespace Builder.Services;

public interface IPortResolver
{
    List<PortMapping> ResolveAll(FeatureSelection features, ISystemChecker systemChecker);

    List<PortMapping> AutoRemap(List<PortMapping> mappings);
}

public class PortResolver : IPortResolver
{
    private static readonly int[] FrontendFallbacks = [8080, 8081, 8888];
    private readonly ISystemChecker _fallbackChecker;

    public PortResolver() : this(new SystemChecker())
    {
    }

    // Availability probes during remap must honor the injected checker -
    // touching real sockets made behavior host-dependent (fragile in tests)
    public PortResolver(ISystemChecker fallbackChecker)
    {
        _fallbackChecker = fallbackChecker;
    }

    public List<PortMapping> ResolveAll(FeatureSelection features, ISystemChecker systemChecker)
    {
        var mappings = new List<PortMapping>
        {
            CreateMapping("HTTP", "HTTP_PORT", 80, systemChecker),
            CreateMapping("HTTPS", "HTTPS_PORT", 443, systemChecker),
        };

        if (features.IsEnabled(FeatureCatalog.Monitoring.Id))
            mappings.Add(CreateMapping("Grafana", "GRAFANA_PORT", 3000, systemChecker));

        return mappings;
    }

    public List<PortMapping> AutoRemap(List<PortMapping> mappings)
    {
        foreach (var mapping in mappings.Where(m => m.IsConflicted))
        {
            var newPort = FindAvailablePort(mapping.DefaultPort, mappings);
            if (newPort.HasValue)
            {
                mapping.AssignedPort = newPort.Value;
                mapping.IsConflicted = false;
            }
        }

        return mappings;
    }

    private static PortMapping CreateMapping(string serviceName, string portKey, int port, ISystemChecker systemChecker)
    {
        var isAvailable = systemChecker.IsPortAvailable(port);
        return new PortMapping
        {
            ServiceName = serviceName,
            PortKey = portKey,
            DefaultPort = port,
            AssignedPort = port,
            IsConflicted = !isAvailable,
            ConflictProcess = isAvailable ? null : systemChecker.GetProcessUsingPort(port)
        };
    }

    private int? FindAvailablePort(int defaultPort, List<PortMapping> existingMappings)
    {
        var usedPorts = existingMappings.Select(m => m.AssignedPort).ToHashSet();

        if (defaultPort == 80)
        {
            foreach (var fallback in FrontendFallbacks)
            {
                if (!usedPorts.Contains(fallback) && _fallbackChecker.IsPortAvailable(fallback))
                    return fallback;
            }
        }

        for (var candidate = defaultPort + 1; candidate < defaultPort + 100; candidate++)
        {
            if (!usedPorts.Contains(candidate) && _fallbackChecker.IsPortAvailable(candidate))
                return candidate;
        }

        for (var candidate = 49152; candidate <= 65535; candidate++)
        {
            if (!usedPorts.Contains(candidate) && _fallbackChecker.IsPortAvailable(candidate))
                return candidate;
        }

        return null;
    }
}
