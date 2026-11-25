using System.ComponentModel;
using System.Text.RegularExpressions;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Processes;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming.VideoEncoding;

/// <summary>
/// Probes a render node with the same ffmpeg, driver and device access a transcode would use:
/// one black frame uploaded to the GPU and encoded with <c>h264_vaapi</c>.
/// </summary>
public sealed partial class VaapiEncoderProbe(ILogger<VaapiEncoderProbe> logger) : IGpuEncoderProbe
{
    // A working GPU answers in well under a second; a hung driver must not hold up startup.
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private const int DetailLines = 3;

    /// <inheritdoc/>
    public async Task<GpuProbeResult> ProbeAsync(string device, CancellationToken ct)
    {
        if (!File.Exists(device))
            return new GpuProbeResult(device, false, null, $"{device} does not exist in this container.");

        // ffmpeg reports a node it may not open as "Invalid argument", which hides the usual cause:
        // the container is missing the group that owns the device on the host.
        if (AccessProblem(device) is { } problem)
            return new GpuProbeResult(device, false, null, problem);

        try
        {
            var result = await ExternalProcess.RunAsync("ffmpeg", Arguments(device), Timeout, logger, ct);

            return Interpret(device, result);
        }
        catch (ExternalProcessTimeoutException)
        {
            return new GpuProbeResult(device, false, null, $"The test encode did not finish within {Timeout.TotalSeconds:0} s.");
        }
        catch (Win32Exception ex)
        {
            return new GpuProbeResult(device, false, null, $"ffmpeg could not be started: {ex.Message}");
        }
    }

    private static string? AccessProblem(string device)
    {
        try
        {
            using var handle = File.OpenHandle(device, FileMode.Open, FileAccess.ReadWrite);

            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return $"No permission to open {device}. The container needs the group that owns it on the host.";
        }
        catch (IOException ex)
        {
            return $"{device} could not be opened: {ex.Message}";
        }
    }

    // Verbose logging is what reports the VAAPI driver; the whole output stays a few KB.
    internal static string Arguments(string device) =>
        $"-hide_banner -nostdin -v verbose -init_hw_device \"vaapi=va:{device}\" -filter_hw_device va " +
        "-f lavfi -i color=black:s=256x144:r=1:d=1 -vf format=nv12,hwupload -frames:v 1 -c:v h264_vaapi -f null -";

    internal static GpuProbeResult Interpret(string device, ExternalProcessResult result)
    {
        var driver = DriverLine().Match(result.StdErrTail) is { Success: true } match
            ? match.Groups["driver"].Value.TrimEnd('.')
            : null;

        if (result.ExitCode == 0)
            return new GpuProbeResult(device, true, driver, null);

        var lines = result.StdErrTail
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => LogPrefix().Replace(line, string.Empty))
            .TakeLast(DetailLines);

        var detail = string.Join(" ", lines);

        if (string.IsNullOrWhiteSpace(detail))
            detail = $"ffmpeg exited with code {result.ExitCode}.";

        return new GpuProbeResult(device, false, driver, detail);
    }

    [GeneratedRegex(@"VAAPI driver: (?<driver>[^\r\n]+)")]
    private static partial Regex DriverLine();

    [GeneratedRegex(@"^\[[^\]]+ @ 0x[0-9a-f]+\]\s*")]
    private static partial Regex LogPrefix();
}
