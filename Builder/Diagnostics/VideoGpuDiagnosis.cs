using Builder.Catalog;
using Builder.Common;
using Builder.Configuration;
using Builder.Deployment;
using Builder.Docker;
using Builder.SystemCheck;
using Builder.Workflow;

namespace Builder.Diagnostics;

// Reports which of this machine's GPUs the transcoding worker can encode on, and whether the one
// an installation is wired to still works. The installed worker image is tested when there is one;
// otherwise a temporary one can be built from source. The installation is only read: the probes
// run in throwaway containers and no settings are written.
public sealed class VideoGpuDiagnosis(ISystemChecker checker, IDockerService docker)
{
    // Its own name, so a test build never replaces the image an installation runs.
    internal const string TemporaryImage = "alexandria-gpu-check";

    private const string RunSetupAgain = "Run setup again with this folder to rewire it.";

    // checkoutRoot is the source to build from when the installation names none that can be used.
    // Without buildMissingImage nothing is built and the report says what could be built from.
    public VideoGpuReport Run(string folder, string? checkoutRoot = null, bool buildMissingImage = false,
        Action<string>? onBuildOutput = null, CancellationToken cancellationToken = default)
    {
        if (checker.GetDockerContextError() is { } contextError)
        {
            return new VideoGpuReport { Folder = folder, Notes = [contextError], Incomplete = true };
        }

        var resources = checker.GetSystemResources();
        var report = new VideoGpuReport
        {
            Folder = folder,
            Nodes = resources.VideoGpus.Select(node => new VideoGpuNodeReport(node, null, null, false)).ToArray(),
        };
        var notes = new List<string>();

        if (resources.VideoGpus.Count == 0)
        {
            notes.Add(resources.VideoGpuDetectionError ?? resources.VideoGpuUnavailableReason ??
                "No AMD or Intel GPU was found on this machine.");
        }

        InstallationInspection inspection;

        try
        {
            inspection = InstallationConfigurationResolver.Inspect(new InstallationContext { InstallPath = folder });

            if (inspection is { SavedConfiguration: { } saved, IsLegacy: false })
            {
                docker.Configure(saved, cancellationToken);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException
                                          or FormatException or System.Text.Json.JsonException)
        {
            notes.Add("The installation could not be read: " + error.Message);

            return report with { Notes = notes, Incomplete = true };
        }

        var environment = inspection.ExistingEnvironment;
        var redactor = new SecretRedactor(() => environment);

        VideoGpuReport WithTemporaryImage(VideoGpuReport current) => TestWithTemporaryImage(current, notes,
            [inspection.SavedConfiguration?.SourceRoot, checkoutRoot], redactor, buildMissingImage, onBuildOutput, cancellationToken);

        if (inspection.SavedConfiguration is not { } config)
        {
            notes.Add("There is no Alexandria installation in this folder.");

            return WithTemporaryImage(report);
        }

        if (inspection.IsLegacy)
        {
            notes.Add("This installation predates GPU support. Run setup again with this folder to give its worker that.");

            return WithTemporaryImage(report);
        }

        if (!VideoEncodingEnvironment.Applies(ServiceCatalog.Resolve(config.Features)))
        {
            notes.Add("Adaptive streaming is not part of this installation, so it encodes no video.");

            return report with { Notes = notes };
        }

        // Every setup that knows about GPUs writes the mode. Without it the image is older too,
        // and an old worker given --probe-gpu would start as a normal worker.
        if (!environment.TryGetValue(VideoEncodingEnvironment.ModeKey, out var mode))
        {
            notes.Add("This installation's settings have no video encoder yet. Run setup again with this folder to give its worker one.");

            return WithTemporaryImage(report);
        }

        var wiredDevice = mode.Equals("Cpu", StringComparison.OrdinalIgnoreCase)
            ? null
            : environment.GetValueOrDefault(VideoEncodingEnvironment.DeviceKey);
        var wiredNode = resources.VideoGpus.FirstOrDefault(node => node.Path == wiredDevice);
        report = report with
        {
            Setting = Describe(wiredDevice, wiredNode),
            Nodes = report.Nodes.Select(node => node with { InUse = node.Node == wiredNode }).ToArray(),
        };

        if (wiredDevice is not null && wiredNode is null)
        {
            notes.Add($"The installation is wired to {wiredDevice}, which was not found on this machine. {RunSetupAgain}");
        }
        else if (wiredNode is not null && !SameAccess(environment, wiredNode))
        {
            notes.Add($"The GPU's device number or group changed since setup, so the worker may not open it. {RunSetupAgain}");
        }

        // Compose names an image it builds after the project and the service.
        var installedImage = $"{config.ProjectName}-{VideoEncodingEnvironment.TranscoderId}";
        var inspected = docker.InspectImage(installedImage, cancellationToken);

        if (!inspected.Succeeded)
        {
            if (!inspected.StandardError.Contains("No such image", StringComparison.OrdinalIgnoreCase))
            {
                notes.Add("Docker could not be asked about the worker image: " +
                          redactor.Redact(VideoGpuTest.FailureReason(inspected)));

                return report with { Notes = notes, Incomplete = true };
            }

            notes.Add("This installation's worker image is not built, so its worker was not checked.");
            var tested = WithTemporaryImage(report);

            // The worker itself was not probed, so the wired GPU's own result stands in for it.
            var wiredResult = tested.Nodes.FirstOrDefault(node => node.Node == wiredNode);
            var wiredFails = wiredDevice is not null && (wiredNode is null || wiredResult?.Encodes == false);

            return WithUnusedGpuNote(tested with { WiredGpuFails = wiredFails }, wiredDevice, notes);
        }

        var nodes = TestNodes(report.Nodes, installedImage, redactor, cancellationToken);
        report = report with { Nodes = nodes, Incomplete = NoneTested(nodes) };

        if (wiredDevice is null)
        {
            return WithUnusedGpuNote(report with { Notes = notes }, wiredDevice, notes);
        }

        var worker = Test(wiredDevice, docker.ProbeVideoGpus(folder, [wiredDevice]), redactor);

        return report with
        {
            WorkerEncodes = worker.Encodes,
            WorkerDetail = worker.Detail,
            WiredGpuFails = worker.Encodes != true,
            Notes = notes,
        };
    }

    private VideoGpuReport TestWithTemporaryImage(VideoGpuReport report, List<string> notes, IReadOnlyList<string?> sources,
        SecretRedactor redactor, bool build, Action<string>? onBuildOutput, CancellationToken cancellationToken)
    {
        // Without a GPU there is nothing a build could answer.
        if (report.Nodes.Count == 0) return report with { Notes = notes };

        var source = sources.OfType<string>().FirstOrDefault(CanBuildFrom);

        if (source is null)
        {
            notes.Add("No Alexandria source with the transcoding worker was found, so the GPUs could not be tested. " +
                      "Run the Builder from an Alexandria checkout.");

            return report with { Notes = notes, Incomplete = true };
        }

        if (!build) return report with { Notes = notes, BuildSource = source };

        onBuildOutput?.Invoke($"Building the transcoding worker from {source} for this test. " +
                              "The first build can take several minutes; Ctrl+C cancels.");

        var errors = new List<string>();

        // TODO: pull a pre-built worker image instead once they are published, and compile only
        // when asked to build from source.
        var built = docker.BuildTranscoderImage(source, TemporaryImage, line =>
        {
            if (line.Contains("error", StringComparison.OrdinalIgnoreCase)) errors.Add(line);

            onBuildOutput?.Invoke(line);
        }, cancellationToken);

        if (!built.Succeeded)
        {
            var reason = errors.LastOrDefault() ?? VideoGpuTest.FailureReason(built);
            notes.Add("The worker image could not be built, so no GPU was tested: " + redactor.Redact(reason));

            return report with { Notes = notes, BuildFailed = true };
        }

        try
        {
            report = report with { Nodes = TestNodes(report.Nodes, TemporaryImage, redactor, cancellationToken) };
        }
        finally
        {
            // Not cancellable, so a cancelled test does not leave the image behind. Docker keeps
            // the build cache, which makes the next build from this source quick.
            if (!docker.RemoveImage(TemporaryImage, CancellationToken.None).Succeeded)
            {
                notes.Add($"The temporary image could not be removed. Remove it with: docker image rm {TemporaryImage}");
            }
        }

        notes.Add($"Tested with a temporary worker image built from {source}.");

        return report with { Notes = notes, Incomplete = NoneTested(report.Nodes) };
    }

    private static bool NoneTested(IReadOnlyList<VideoGpuNodeReport> nodes) =>
        nodes.Count > 0 && nodes.All(node => node.Encodes is null);

    private VideoGpuNodeReport[] TestNodes(IReadOnlyList<VideoGpuNodeReport> nodes, string image, SecretRedactor redactor,
        CancellationToken cancellationToken) =>
        nodes.Select(node =>
        {
            var result = docker.ProbeVideoGpuInImage(image, node.Node, cancellationToken);
            var (encodes, detail) = Test(node.Node.Path, result, redactor);

            return node with { Encodes = encodes, Detail = detail };
        }).ToArray();

    // A source older than GPU support has no probe, and its worker would just start.
    private static bool CanBuildFrom(string source)
    {
        var worker = Path.Combine(source, "Backend", "Alexandria.Workers.MediaTranspilation");

        return File.Exists(Path.Combine(worker, "Dockerfile")) && File.Exists(Path.Combine(worker, "GpuProbeCommand.cs"));
    }

    private static VideoGpuReport WithUnusedGpuNote(VideoGpuReport report, string? wiredDevice, List<string> notes)
    {
        if (wiredDevice is null && report.Nodes.Any(node => node.Encodes == true))
        {
            notes.Add("A GPU here can encode, but this installation does not use it. " + RunSetupAgain);
        }

        return report with { Notes = notes };
    }

    private static (bool? Encodes, string? Detail) Test(string device, ProcessResult result, SecretRedactor redactor)
    {
        var entry = VideoGpuTestReport.Parse(result.StandardOutput)?.FirstOrDefault(found => found.Device == device);

        if (entry is null)
        {
            return (null, "The test did not run: " + redactor.Redact(VideoGpuTest.FailureReason(result)));
        }

        if (entry.Usable)
        {
            return (true, entry.Driver);
        }

        return (false, entry.Detail ?? "The worker gave no reason.");
    }

    private static string Describe(string? device, RenderNode? node)
    {
        if (device is null) return "Encodes video on the CPU";

        return "Encodes video on the " + (node?.Description ?? "device " + device);
    }

    private static bool SameAccess(IReadOnlyDictionary<string, string> environment, RenderNode node) =>
        environment.GetValueOrDefault(VideoEncodingEnvironment.DeviceNumberKey) == node.DeviceNumber &&
        environment.GetValueOrDefault(VideoEncodingEnvironment.GroupKey) ==
        node.GroupId.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
