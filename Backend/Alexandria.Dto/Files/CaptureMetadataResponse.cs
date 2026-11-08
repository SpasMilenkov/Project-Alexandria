namespace Alexandria.Dto.Files;

public sealed class CaptureMetadataResponse
{
    public required Guid FileVersionId { get; init; }

    /// <summary>
    /// False when the version was not read yet or cannot be read, for example because it is
    /// encrypted. Empty fields then mean "unknown", not "this file has none".
    /// </summary>
    public required bool IsExtracted { get; init; }

    public required bool HasLocation { get; init; }

    public int? Width { get; init; }
    public int? Height { get; init; }

    /// <summary>
    /// A true instant only when <see cref="CapturedOffsetMinutes"/> is set. Otherwise this is the
    /// camera's wall clock, despite being serialized as UTC.
    /// </summary>
    public DateTime? CapturedAt { get; init; }
    public short? CapturedOffsetMinutes { get; init; }

    public double? Latitude { get; init; }
    public double? Longitude { get; init; }

    public string? CameraMake { get; init; }
    public string? CameraModel { get; init; }
    public string? LensModel { get; init; }
    public string? Software { get; init; }

    public double? FocalLengthMm { get; init; }
    public int? FocalLength35mm { get; init; }
    public double? FNumber { get; init; }
    public double? ExposureSeconds { get; init; }
    public int? Iso { get; init; }
}
