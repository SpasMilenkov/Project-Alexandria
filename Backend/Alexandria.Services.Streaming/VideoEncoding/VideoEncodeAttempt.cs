namespace Alexandria.Services.Streaming.VideoEncoding;

/// <summary>
/// One way of running the video DASH pass, from the cheapest on the CPU to the one that always
/// works.
/// </summary>
internal enum VideoEncodeAttempt
{
    /// <summary>The GPU decodes, scales and encodes.</summary>
    GpuDecodeAndEncode,

    /// <summary>The CPU decodes; the GPU scales and encodes. Covers codecs the GPU cannot decode.</summary>
    CpuDecodeGpuEncode,

    /// <summary>libx264 on the CPU.</summary>
    Cpu,
}
