using Alexandria.Common.Exceptions.Transpilation;

namespace Alexandria.Services.Streaming.VideoEncoding;

/// <summary>
/// Runs the video pass with each attempt in turn until one succeeds, so a GPU problem costs
/// time but never fails a job the CPU could have finished.
/// </summary>
internal static class VideoEncodeFallback
{
    private static readonly VideoEncodeAttempt[] GpuAttempts =
    [
        VideoEncodeAttempt.GpuDecodeAndEncode,
        VideoEncodeAttempt.CpuDecodeGpuEncode,
        VideoEncodeAttempt.Cpu,
    ];

    private static readonly VideoEncodeAttempt[] CpuAttempts = [VideoEncodeAttempt.Cpu];

    public static IReadOnlyList<VideoEncodeAttempt> AttemptsFor(VideoEncoderSelection selection) =>
        selection.UsesGpu ? GpuAttempts : CpuAttempts;

    /// <summary>
    /// Only an ffmpeg failure moves on to the next attempt. Cancellation, a timeout and the last
    /// attempt's failure end the pass as they would without a GPU.
    /// </summary>
    public static async Task RunAsync(
        IReadOnlyList<VideoEncodeAttempt> attempts,
        Func<VideoEncodeAttempt, Task> run,
        Action<VideoEncodeAttempt, VideoEncodeAttempt, TranspilationFfmpegException> onFallback)
    {
        for (var i = 0; i < attempts.Count; i++)
        {
            try
            {
                await run(attempts[i]);
                return;
            }
            catch (TranspilationFfmpegException ex) when (i < attempts.Count - 1)
            {
                onFallback(attempts[i], attempts[i + 1], ex);
            }
        }
    }
}
