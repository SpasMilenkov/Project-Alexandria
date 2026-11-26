namespace Alexandria.Services.Streaming.VideoEncoding;

/// <summary>
/// Chooses the rate control for each GPU attempt of one job: constant quality first, and a capped
/// re-encode when a lane runs past its ceiling. Once constant quality has overshot, a later
/// attempt goes straight to the capped pass. Attempts differ only in where the source is decoded,
/// so the same encoder at the same QP would overshoot again and waste a full encode.
/// </summary>
internal sealed class GpuRatePlan
{
    private bool _overshot;

    public bool KnownToOvershoot => _overshot;

    /// <param name="encode">Runs one GPU pass with the given rate control.</param>
    /// <param name="findProblem">Checks the finished constant-quality output against the ceilings.</param>
    /// <param name="onOvershoot">Runs before the capped pass, with the problem found.</param>
    public async Task RunAsync(
        Func<GpuRateControl, Task> encode,
        Func<string?> findProblem,
        Action<string> onOvershoot)
    {
        if (!_overshot)
        {
            await encode(GpuRateControl.ConstantQuality);

            if (findProblem() is not { } problem)
                return;

            _overshot = true;
            onOvershoot(problem);
        }

        await encode(GpuRateControl.CappedBitrate);
    }
}
