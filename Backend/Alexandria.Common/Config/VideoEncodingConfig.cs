namespace Alexandria.Common.Config;

/// <summary>
/// Encoder choice for video transcoding.
/// Bind from <c>appsettings.json</c> under the <c>"VideoEncoding"</c> section.
/// </summary>
public class VideoEncodingConfig
{
    /// <summary>
    /// <see cref="VideoEncoderMode.Auto"/> encodes on the GPU when <see cref="Device"/> passes
    /// its probe and on the CPU otherwise; <see cref="VideoEncoderMode.Cpu"/> never uses the GPU.
    /// </summary>
    public VideoEncoderMode Mode { get; set; } = VideoEncoderMode.Auto;

    /// <summary>
    /// VAAPI render node used for GPU encoding. The setup maps the chosen host GPU to this path
    /// inside the container.
    /// </summary>
    public string Device { get; set; } = "/dev/dri/renderD128";
}
