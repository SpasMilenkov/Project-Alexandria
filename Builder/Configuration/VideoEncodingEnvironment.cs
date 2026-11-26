using System.Globalization;
using Builder.Catalog;
using Builder.SystemCheck;
using Builder.Workflow;

namespace Builder.Configuration;

// The transcoding worker's video encoder, kept in .env. With a usable GPU the worker is pointed
// at its stable device name, joins the group that owns it and may open that one device; without
// one the worker is told to encode on the CPU and no GPU access is written at all.
public static class VideoEncodingEnvironment
{
    public const string ModeKey = "VIDEO_ENCODING_MODE";
    public const string DeviceKey = "VIDEO_GPU_DEVICE";
    public const string GroupKey = "VIDEO_GPU_GROUP";
    public const string DeviceNumberKey = "VIDEO_GPU_DEVICE_NUMBER";

    public const string TranscoderId = "transpilation-worker";

    public static void Apply(Dictionary<string, string> values, ResolvedCapabilities capabilities, SystemResources resources)
    {
        values.Remove(ModeKey);
        values.Remove(DeviceKey);
        values.Remove(GroupKey);
        values.Remove(DeviceNumberKey);

        if (!Applies(capabilities)) return;

        if (resources.VideoGpus is not [var gpu, ..])
        {
            values[ModeKey] = "Cpu";
            return;
        }

        values[ModeKey] = "Auto";
        values[DeviceKey] = gpu.Path;
        values[GroupKey] = gpu.GroupId.ToString(CultureInfo.InvariantCulture);
        values[DeviceNumberKey] = gpu.DeviceNumber;
    }

    public static bool UsesGpu(IReadOnlyDictionary<string, string> values) => values.ContainsKey(DeviceKey);

    public static bool IsWiredTo(IReadOnlyDictionary<string, string> values, RenderNode gpu) =>
        values.GetValueOrDefault(DeviceKey) == gpu.Path &&
        values.GetValueOrDefault(DeviceNumberKey) == gpu.DeviceNumber &&
        values.GetValueOrDefault(GroupKey) == gpu.GroupId.ToString(CultureInfo.InvariantCulture);

    public static bool Applies(ResolvedCapabilities capabilities) =>
        capabilities.ServiceIds.Contains(TranscoderId, StringComparer.Ordinal);

    // What the review promises, before the GPU has been tried in the built image.
    public static string Plan(SystemResources resources) => resources.VideoGpus is [var gpu, ..]
        ? $"On the {gpu.Description}, if it passes a test after the build; on the CPU otherwise"
        : "On the CPU, because " + NoGpuReason(resources);

    // What the finished installation does, or null when streaming is not selected.
    public static string? Outcome(InstallationContext context)
    {
        if (!Applies(ServiceCatalog.Resolve(context.Features))) return null;

        return context.VideoEncodingOutcome ?? "On the CPU, because " + NoGpuReason(context.Resources);
    }

    private static string NoGpuReason(SystemResources resources) =>
        resources.VideoGpuDetectionError ?? resources.VideoGpuUnavailableReason ?? "no AMD or Intel GPU was found";
}
