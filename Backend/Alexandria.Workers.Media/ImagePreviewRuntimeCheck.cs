using Alexandria.Common.Config;
using Alexandria.Services.Preview.Images;
using Microsoft.Extensions.Options;

namespace Alexandria.Workers.Media;

/// <summary>
/// Applies the libvips hardening and probes the ImageMagick fallback at startup, so a broken
/// native setup shows up in the logs before the first preview job instead of during it.
/// </summary>
public partial class ImagePreviewRuntimeCheck(
    IOptions<ImagePreviewConfig> options,
    MagickCliDecoder magick,
    ILogger<ImagePreviewRuntimeCheck> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        VipsRuntime.EnsureConfigured(options.Value);
        LogVipsReady(logger, VipsRuntime.Version);

        await magick.IsAvailableAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(4260, LogLevel.Information, "libvips {Version} configured for image previews")]
    private static partial void LogVipsReady(ILogger logger, string version);
}
