using Builder.Models;
using Builder.Services;

namespace Builder.UI.Shell.Screens;

// The sidebar's cost summary. Once the system check has measured Docker, the
// memory line compares the selection against Docker's limit, so later
// screens and a return to the feature list keep showing whether it fits.
internal static class SidebarStats
{
    public static void Push(Shell shell, IResourceCalculator calculator)
    {
        var features = shell.Context.Features;
        var resources = shell.Context.Resources;
        var capabilities = ServiceCatalog.Resolve(features);
        var neededMb = calculator.CalculateMinimumRamMb(features);
        var memoryMb = ResourceCalculator.MemoryLimitMb(resources);
        var cores = ResourceCalculator.CpuLimit(resources);

        shell.UpdateSidebarStats(stats =>
        {
            stats.Clear();

            if (memoryMb > 0)
            {
                stats.Add(new SidebarStat
                {
                    Label = "Memory",
                    Value = $"{neededMb / 1024.0:0.#} of {memoryMb / 1024.0:0.#} GB",
                    Tone = MachineFit.Tone(MachineFit.Memory(memoryMb, neededMb)),
                });
            }
            else
            {
                stats.Add(new SidebarStat { Label = "RAM estimate", Value = $"{neededMb / 1024.0:0.#} GB" });
            }

            if (cores > 0)
            {
                stats.Add(new SidebarStat
                {
                    Label = "CPU",
                    Value = $"{cores} cores",
                    Tone = MachineFit.Tone(MachineFit.Cpu(cores, ResourceCalculator.RecommendedCpuCores(features))),
                });
            }

            stats.Add(new SidebarStat { Label = "Services", Value = $"{capabilities.RunningServiceCount} ongoing" });
            stats.Add(new SidebarStat { Label = "Setup jobs", Value = $"{capabilities.OneShotServiceIds.Count} run once" });
        });
    }
}
