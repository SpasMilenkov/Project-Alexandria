using AwesomeAssertions;
using Builder.Catalog;
using Builder.Configuration;
using Builder.SystemCheck;

namespace Builder.Tests.SystemCheck;

public sealed class ResourceBudgetTests
{
    private static FeatureSelection Everything(string backbone = "maest")
    {
        var features = new FeatureSelection
        {
            Enabled = FeatureCatalog.ToggleFeatures.Select(feature => feature.Id).ToHashSet(StringComparer.Ordinal),
        };
        features.SetAudioBackbone(backbone);

        return features;
    }

    private static SystemResources Machine(long memoryMb, int cores) => new()
    {
        CpuCores = cores,
        TotalMemoryMb = memoryMb,
        DockerCpuCores = cores,
        DockerMemoryMb = memoryMb,
    };

    private static Dictionary<string, ServiceAllocation> Plan(FeatureSelection features, SystemResources resources) =>
        ResourceBudget.Plan(ServiceCatalog.Resolve(features), resources).ToDictionary(allocation => allocation.ServiceId);

    private static ServiceInfo Service(string id) => ServiceCatalog.All.Single(service => service.Id == id);

    [Fact]
    public void Plan_unmeasuredMachine_keepsCatalogCaps()
    {
        var plan = Plan(Everything(), new SystemResources());

        foreach (var service in ServiceCatalog.Resolve(Everything()).Services.Where(service => !service.OneShot))
        {
            plan[service.Id].MemoryMb.Should().Be(service.MemoryMb);
            plan[service.Id].Cpus.Should().Be(service.Cpus);
        }
    }

    [Theory]
    [InlineData(32768, 16)]
    [InlineData(13812, 16)]
    public void Plan_machineWithRoom_keepsCatalogCapsAndLeavesTheReservedCoresFree(long memoryMb, int cores)
    {
        var plan = Plan(Everything(), Machine(memoryMb, cores));

        foreach (var allocation in plan.Values)
        {
            allocation.MemoryMb.Should().Be(Service(allocation.ServiceId).MemoryMb);
        }

        plan["essentia-maest-worker"].Cpus.Should().Be(4);
        plan.Values.Sum(allocation => allocation.MemoryMb).Should().BeLessThanOrEqualTo(ResourceBudget.UsableMemoryMb(memoryMb));
        HeavyCores(plan).Should().BeLessThanOrEqualTo(ResourceBudget.UsableCpuCores(cores));
    }

    [Theory]
    [InlineData(12288, 8)]
    [InlineData(11264, 8)]
    [InlineData(10240, 6)]
    public void Plan_tightMachine_shrinksFlexibleWorkersToFitAndKeepsFixedCaps(long memoryMb, int cores)
    {
        var plan = Plan(Everything(), Machine(memoryMb, cores));

        plan.Values.Sum(allocation => allocation.MemoryMb).Should().BeLessThanOrEqualTo(ResourceBudget.UsableMemoryMb(memoryMb));
        plan["essentia-maest-worker"].MemoryMb.Should().Be(2048);
        plan["postgres"].MemoryMb.Should().Be(Service("postgres").MemoryMb);
        plan["transpilation-worker"].MemoryMb.Should().BeLessThan(Service("transpilation-worker").MemoryMb);
        HeavyCores(plan).Should().BeLessThanOrEqualTo(ResourceBudget.UsableCpuCores(cores));

        foreach (var allocation in plan.Values.Where(allocation => allocation.MemoryMb != Service(allocation.ServiceId).MemoryMb))
        {
            allocation.MemoryMb.Should().BeGreaterThanOrEqualTo(Service(allocation.ServiceId).MinimumMemoryMb);
            (allocation.MemoryMb % 64).Should().Be(0);
        }
    }

    [Fact]
    public void Plan_smallMachine_stopsAtMinimumsAndGivesHeavyJobsWholeCores()
    {
        var plan = Plan(Everything(), Machine(4096, 2));

        plan["media-worker"].MemoryMb.Should().Be(Service("media-worker").MinimumMemoryMb);
        plan["lyrics-worker"].MemoryMb.Should().Be(Service("lyrics-worker").MinimumMemoryMb);
        plan.Values.Where(allocation => Service(allocation.ServiceId).HeavyCpu).Should().HaveCount(2)
            .And.OnlyContain(allocation => allocation.Cpus == 1);

        plan.Values.Max(allocation => allocation.Cpus).Should().BeLessThanOrEqualTo(ResourceBudget.UsableCpuCores(2));
    }

    [Fact]
    public void Plan_cpuCapsNeverExceedTheUsableCores()
    {
        var plan = Plan(Everything("effnet"), Machine(65536, 4));

        plan.Values.Should().OnlyContain(allocation => allocation.Cpus <= ResourceBudget.UsableCpuCores(4));
        HeavyCores(plan).Should().BeLessThanOrEqualTo(ResourceBudget.UsableCpuCores(4));
        plan["essentia-effnet-worker"].Cpus.Should().Be(1);
        plan["transpilation-worker"].Cpus.Should().Be(1);
        plan["frontend"].Cpus.Should().Be(0.5);
    }

    [Theory]
    [InlineData(4096, 2048)]
    [InlineData(16384, 2457)]
    [InlineData(65536, 4096)]
    public void HostReserve_scalesWithTheMachineWithinItsBounds(long machineMb, long expectedMb)
    {
        ResourceBudget.HostReserveMb(machineMb).Should().Be(expectedMb);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(4, 3)]
    [InlineData(16, 14)]
    public void UsableCpuCores_keepsCoresFreeForTheHost(int cores, int expected)
    {
        ResourceBudget.UsableCpuCores(cores).Should().Be(expected);
    }

    [Fact]
    public void EnvironmentValues_formatsComposeReadyCaps()
    {
        var values = ResourceBudget.EnvironmentValues(
        [
            new ServiceAllocation { ServiceId = "media-metadata-worker", MemoryMb = 768, Cpus = 1 },
            new ServiceAllocation { ServiceId = "frontend", MemoryMb = 128, Cpus = 0.5 },
        ]);

        values.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["LIMIT_MEDIA_METADATA_WORKER_MEMORY"] = "768M",
            ["LIMIT_MEDIA_METADATA_WORKER_CPUS"] = "1",
            ["LIMIT_FRONTEND_MEMORY"] = "128M",
            ["LIMIT_FRONTEND_CPUS"] = "0.5",
        });
    }

    // The template fallbacks apply when compose runs without the generated .env, so they must
    // match the catalog, and every running service must be capped with swap held to its cap.
    [Theory]
    [InlineData("effnet")]
    [InlineData("maest")]
    public void Templates_capEveryRunningServiceWithTheCatalogDefaults(string backbone)
    {
        var features = Everything(backbone);
        var config = new InstallationConfig { Features = features, Ports = new() { ["HTTPS_PORT"] = 8443 } };
        var compose = new ConfigurationService(new TemplateService("Local")).GenerateDockerCompose(config);

        foreach (var service in ServiceCatalog.Resolve(features).Services.Where(service => !service.OneShot))
        {
            var memory = $"${{{ResourceBudget.MemoryKey(service.Id)}:-{ResourceBudget.FormatMemory(service.MemoryMb)}}}";
            var cpus = $"${{{ResourceBudget.CpuKey(service.Id)}:-{ResourceBudget.FormatCpus(service.Cpus)}}}";

            compose.Should().Contain($"memswap_limit: {memory}", service.Id);
            compose.Should().Contain($"memory: {memory}", service.Id);
            compose.Should().Contain($"cpus: \"{cpus}\"", service.Id);
        }
    }

    private static double HeavyCores(Dictionary<string, ServiceAllocation> plan) =>
        plan.Values.Where(allocation => Service(allocation.ServiceId).HeavyCpu).Sum(allocation => allocation.Cpus);
}
