using Alexandria.Services.Streaming.VideoEncoding;

namespace Alexandria.Workers.MediaTranspilation;

/// <summary>
/// Settles the encoder choice when the worker starts, so the log says GPU or CPU before the
/// first job arrives. It runs in the background because a slow driver must not delay startup.
/// </summary>
public sealed class VideoEncoderStartupCheck(IVideoEncoderSelector selector) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => selector.SelectAsync(stoppingToken);
}
