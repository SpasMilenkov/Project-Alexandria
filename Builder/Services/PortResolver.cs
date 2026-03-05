using System.Net.NetworkInformation;
using Builder.Models;
using Environment = Builder.Models.Environment;

namespace Builder.Services;

public interface IPortResolver
{
    List<PortMapping> ResolveAll(FeatureSelection features, Environment environment, ISystemChecker systemChecker);
    List<PortMapping> AutoRemap(List<PortMapping> mappings);
}

public class PortResolver : IPortResolver
{
    private static readonly int[] FrontendFallbacks = [8080, 3000, 8888];

    public List<PortMapping> ResolveAll(FeatureSelection features, Environment environment, ISystemChecker systemChecker)
    {
        var mappings = new List<PortMapping>();

        mappings.Add(CreateMapping("Frontend", "FRONTEND_PORT", features.FrontendPort, systemChecker));

        if (features.Monitoring)
            mappings.Add(CreateMapping("Grafana", "GRAFANA_PORT", features.GrafanaPort, systemChecker));

        // Local Preview exposes additional debug ports
        if (environment == Environment.LocalPreview)
        {
            mappings.Add(CreateMapping("PostgreSQL", "POSTGRES_PORT", features.PostgresPort, systemChecker));
            mappings.Add(CreateMapping("RabbitMQ Mgmt", "RABBITMQ_MANAGEMENT_PORT", features.RabbitmqManagementPort, systemChecker));
        }

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

    private static int? FindAvailablePort(int defaultPort, List<PortMapping> existingMappings)
    {
        var usedPorts = existingMappings.Select(m => m.AssignedPort).ToHashSet();

        if (defaultPort == 80)
        {
            foreach (var fallback in FrontendFallbacks)
            {
                if (!usedPorts.Contains(fallback) && IsPortAvailable(fallback))
                    return fallback;
            }
        }

        for (var candidate = defaultPort + 1; candidate < defaultPort + 100; candidate++)
        {
            if (!usedPorts.Contains(candidate) && IsPortAvailable(candidate))
                return candidate;
        }

        for (var candidate = 49152; candidate <= 65535; candidate++)
        {
            if (!usedPorts.Contains(candidate) && IsPortAvailable(candidate))
                return candidate;
        }

        return null;
    }

    private static bool IsPortAvailable(int port)
    {
        var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
        var tcpListeners = ipGlobalProperties.GetActiveTcpListeners();
        return !tcpListeners.Any(endpoint => endpoint.Port == port);
    }
}
