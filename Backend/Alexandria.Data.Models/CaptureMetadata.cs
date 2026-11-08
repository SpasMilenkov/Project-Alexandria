namespace Alexandria.Data.Models;

/// <summary>
/// How a file version was captured, read from its embedded metadata. Kept per version because a
/// share link can pin an older version whose location differs from the current one.
/// A row with empty fields means the version was read and carries nothing, which stops the
/// backfill from reading it again. A missing row means it was not read yet, or cannot be read
/// because it is encrypted.
/// </summary>
public class CaptureMetadata : IBase
{
    public Guid Id { get; set; }

    public Guid FileVersionId { get; set; }
    public FileVersion? FileVersion { get; set; }

    /// <summary>
    /// Display dimensions, after orientation is applied. Null when the format has no readable
    /// header, because the fallback decoder only ever sees a downscaled copy.
    /// </summary>
    public int? Width { get; set; }
    public int? Height { get; set; }

    /// <summary>
    /// A true instant when <see cref="CapturedOffsetMinutes"/> is set. Without an offset this
    /// holds the camera's wall clock, so it is right for display but not an exact instant.
    /// </summary>
    public DateTime? CapturedAt { get; set; }
    public short? CapturedOffsetMinutes { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public string? CameraMake { get; set; }
    public string? CameraModel { get; set; }
    public string? LensModel { get; set; }
    public string? Software { get; set; }

    public double? FocalLengthMm { get; set; }
    public int? FocalLength35mm { get; set; }
    public double? FNumber { get; set; }
    public double? ExposureSeconds { get; set; }
    public int? Iso { get; set; }

    /// <summary>
    /// The other header fields, so a new column can be filled without reading every original
    /// again. Holds the full GPS block and serial numbers, so it is never returned by the API,
    /// and anything that removes location has to clear it too.
    /// </summary>
    public required string RawJson { get; set; }

    /// <summary>
    /// Which extraction logic wrote this row. Raising the current version makes the backfill
    /// process the row again.
    /// </summary>
    public int ExtractorVersion { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
