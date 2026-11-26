namespace Alexandria.Services.Streaming.VideoEncoding;

/// <summary>
/// How the GPU encoder spends bits. AMD's encoder offers no quality target with a ceiling, so
/// the two are separate passes.
/// </summary>
internal enum GpuRateControl
{
    /// <summary>
    /// Constant QP at the lane's quality target. Bits follow the picture, so static footage stays
    /// small, but nothing stops a busy lane from passing its ceiling.
    /// </summary>
    ConstantQuality,

    /// <summary>
    /// VBR at the lane's ceiling. Never passes it, but spends the whole budget even on footage
    /// that needs a fraction of it.
    /// </summary>
    CappedBitrate,
}
