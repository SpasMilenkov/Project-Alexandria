namespace Alexandria.Common.Config;

/// <summary>
/// Bounds for the out-of-process ImageMagick decoder used for formats libvips cannot load.
/// Bind from <c>appsettings.json</c> under the <c>"ImagePreview:Magick"</c> section.
/// </summary>
public class ImageMagickConfig
{
    public bool Enabled { get; set; } = true;

    public string BinaryPath { get; set; } = "magick";

    /// <summary>
    /// util-linux <c>prlimit</c>, used to apply kernel-enforced limits to the child process.
    /// </summary>
    public string PrlimitPath { get; set; } = "prlimit";

    public int TimeoutSeconds { get; set; } = 30;

    public int CpuSeconds { get; set; } = 30;

    public long AddressSpaceBytes { get; set; } = 1L << 30;

    /// <summary>
    /// Kernel cap on any single file the child writes, which bounds the ImageMagick disk cache.
    /// </summary>
    public long MaxFileBytes { get; set; } = 1L << 30;

    public long MaxInputBytes { get; set; } = 256L << 20;

    public long MaxOutputBytes { get; set; } = 128L << 20;

    public string MemoryLimit { get; set; } = "256MiB";

    public string MapLimit { get; set; } = "512MiB";

    public string DiskLimit { get; set; } = "1GiB";

    public string AreaLimit { get; set; } = "128MP";

    public string MaxDimension { get; set; } = "16KP";

    /// <summary>
    /// The decoded image is downscaled to fit this box before it is handed back to libvips.
    /// </summary>
    public int IntermediateSize { get; set; } = 2560;
}
