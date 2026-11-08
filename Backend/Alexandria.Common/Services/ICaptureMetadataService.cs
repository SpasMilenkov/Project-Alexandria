namespace Alexandria.Common.Services;

public interface ICaptureMetadataService
{
    /// <summary>
    /// Reads the capture metadata from an image header and stores it for the version. Only the
    /// header is read, so no pixels are decoded and formats libvips cannot decode, like HEIC
    /// without an HEVC plugin, still yield their metadata.
    /// </summary>
    Task ExtractAndStoreAsync(Guid versionId, Stream source, CancellationToken ct = default);
}

public static class CaptureMetadataExtractor
{
    /// <summary>
    /// Raise this when extraction changes, and the backfill reprocesses every older row.
    /// </summary>
    public const int CurrentVersion = 1;
}
