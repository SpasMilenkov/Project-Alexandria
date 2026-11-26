namespace Alexandria.Common.Config;

/// <summary>
/// Which encoder video transcoding may use.
/// </summary>
public enum VideoEncoderMode
{
    /// <summary>Use the GPU when its probe passes, otherwise the CPU.</summary>
    Auto,

    /// <summary>Always encode on the CPU, even when a GPU is available.</summary>
    Cpu,
}
