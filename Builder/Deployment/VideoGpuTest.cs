using System.Text.RegularExpressions;
using Builder.Catalog;
using Builder.Common;
using Builder.Configuration;
using Builder.Docker;
using Builder.SystemCheck;
using Builder.Workflow;

namespace Builder.Deployment;

// Tries the detected GPUs in the freshly built worker image, one at a time and each wired the
// way the worker would get it, since the worker may only open the one GPU it is wired to. The
// first that encodes stays. A GPU that does not encode never fails the install: the worker is
// rewired to the next one or to the CPU, its limits recomputed, and the files regenerated
// before any service starts.
public static partial class VideoGpuTest
{
    public const string StageLabel = "Testing the GPU for video";

    public static void Run(InstallationContext context, ResolvedCapabilities capabilities, IConfigurationService configuration,
        IDockerService docker, Action<string> output)
    {
        var resources = context.Resources;
        var candidates = resources.VideoGpus;
        var lastFailure = "no GPU could encode";

        for (var i = 0; i < candidates.Count; i++)
        {
            var gpu = candidates[i];

            // A retry after a failed rewire can leave the files wired to a later candidate, so the
            // settings decide, not the candidate's position.
            if (!VideoEncodingEnvironment.IsWiredTo(context.Config.EnvironmentValues, gpu))
            {
                output($"Trying {gpu.Description} instead.");
                Rewire(context, capabilities, configuration, docker, candidates.Skip(i).ToArray(), output);
            }

            var result = docker.ProbeVideoGpus(context.InstallPath, [gpu.Path], output);
            var entry = VideoGpuTestReport.Parse(result.StandardOutput)?.FirstOrDefault(found => found.Device == gpu.Path);

            if (entry is { Usable: true })
            {
                var encoder = entry.Driver ?? gpu.Description;
                output($"{gpu.Description} encodes with {encoder}.");
                context.VideoEncodingOutcome = "On the GPU: " + encoder;
                return;
            }

            if (entry is null)
            {
                // Nothing was learnt about the GPU itself, so a later attempt tries it again.
                lastFailure = "the test did not run: " + FailureReason(result);
                output($"{gpu.Description} could not be tested: {FailureReason(result)}");
                continue;
            }

            resources.VideoGpus = resources.VideoGpus.Where(node => node != gpu).ToArray();
            lastFailure = "no GPU could encode";
            output($"{gpu.Description} cannot encode: {entry.Detail ?? "the worker gave no reason"}");
        }

        if (resources.VideoGpus.Count == 0)
        {
            resources.VideoGpuUnavailableReason = "no detected GPU could encode in the test after the build";
        }

        Rewire(context, capabilities, configuration, docker, [], output);
        context.VideoEncodingOutcome = "On the CPU, because " + lastFailure;
        output($"Video will be encoded on the CPU: {lastFailure}. Resource limits were recalculated for that.");
    }

    private static void Rewire(InstallationContext context, ResolvedCapabilities capabilities, IConfigurationService configuration,
        IDockerService docker, IReadOnlyList<RenderNode> gpus, Action<string> output)
    {
        var config = context.Config;
        var target = context.InstallPath;
        var wiring = context.Resources.WithVideoGpus(gpus);

        ResourceBudget.ApplyEnvironment(config.EnvironmentValues, capabilities, wiring);
        VideoEncodingEnvironment.Apply(config.EnvironmentValues, capabilities, wiring);

        configuration.BeginGeneration(config, target);
        EnvGenerator.WriteFile(Path.Combine(target, ".env"), config.EnvironmentValues);
        configuration.WriteAllConfigFiles(config, target);

        if (!docker.ValidateConfiguration(target, output))
            throw new InvalidOperationException("Compose rejected the configuration rewritten after the GPU test.");
    }

    // Compose prints its own progress on stderr, so the last line is often "Container ... Started"
    // rather than the error that stopped the probe.
    internal static string FailureReason(ProcessResult result)
    {
        var lines = result.StandardError
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !ComposeProgressLine().IsMatch(line))
            .ToArray();

        var error = lines.LastOrDefault(line => line.Contains("error", StringComparison.OrdinalIgnoreCase)) ?? lines.LastOrDefault();

        if (error is not null) return error;

        if (result.TimedOut) return "it timed out";

        return $"the worker exited with code {result.ExitCode} without a report";
    }

    [GeneratedRegex(@"^(Container|Network|Volume|Image)\s+\S+\s+(Creating|Created|Starting|Started|Stopping|Stopped|Removing|Removed|Waiting|Healthy|Running|Pulling|Pulled|Recreate|Recreated)\b")]
    private static partial Regex ComposeProgressLine();
}
