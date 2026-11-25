using System.Text.Encodings.Web;
using System.Text.Json;
using Alexandria.Services.Streaming.VideoEncoding;
using Microsoft.Extensions.Logging.Abstractions;

namespace Alexandria.Workers.MediaTranspilation;

/// <summary>
/// <c>--probe-gpu [device ...]</c>: runs the worker's own GPU probe and prints one JSON array of
/// results, so setup and diagnostics test exactly what the worker will use. Without devices it
/// tests every render node the container can see. Exits 0 when at least one device is usable.
/// </summary>
public static class GpuProbeCommand
{
    private const string Flag = "--probe-gpu";
    private const string RenderNodeDirectory = "/dev/dri";

    // Relaxed escaping keeps ffmpeg's messages readable when someone runs the command by hand;
    // the output only ever goes to a terminal or the setup tool, never into a web page.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static async Task<int?> TryRunAsync(string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0 || args[0] != Flag)
            return null;

        var devices = args.Length > 1 ? args[1..] : RenderNodes();
        var probe = new VaapiEncoderProbe(NullLogger<VaapiEncoderProbe>.Instance);
        var results = new List<GpuProbeResult>();

        foreach (var device in devices)
            results.Add(await probe.ProbeAsync(device, ct));

        Console.WriteLine(JsonSerializer.Serialize(results, JsonOptions));

        return results.Any(result => result.Usable) ? 0 : 1;
    }

    private static string[] RenderNodes()
    {
        if (!Directory.Exists(RenderNodeDirectory))
            return [];

        return Directory.GetFiles(RenderNodeDirectory, "renderD*").Order(StringComparer.Ordinal).ToArray();
    }
}
