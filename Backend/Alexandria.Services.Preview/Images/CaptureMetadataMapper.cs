using System.Globalization;
using System.Text.Json;
using Alexandria.Common.Services;
using Alexandria.Data.Models;

namespace Alexandria.Services.Preview.Images;

/// <summary>
/// Turns raw header values into typed columns. Anything malformed becomes null rather than a
/// guess, because a wrong capture date or location is worse than a missing one.
/// </summary>
internal static class CaptureMetadataMapper
{
    private const string ExifDateFormat = "yyyy:MM:dd HH:mm:ss";
    private const int FirstPlausibleYear = 1800;
    private const int MaxOffsetMinutes = 18 * 60;
    private const int MaxTextLength = ValidationConstants.StringLengths.MediumString;
    private const int MaxFractionDigits = 7;

    private static readonly JsonSerializerOptions RawJsonOptions = new() { WriteIndented = false };

    public static CaptureMetadata ToEntity(Guid versionId, CaptureHeader header)
    {
        var fields = header.Fields;
        var isQuarterTurned = header.Orientation is >= 5 and <= 8;
        var (capturedAt, offsetMinutes) = ParseCaptureTime(fields);
        var (latitude, longitude) = ParseLocation(fields);

        return new CaptureMetadata
        {
            Id = Guid.NewGuid(),
            FileVersionId = versionId,
            Width = isQuarterTurned ? header.Height : header.Width,
            Height = isQuarterTurned ? header.Width : header.Height,
            CapturedAt = capturedAt,
            CapturedOffsetMinutes = offsetMinutes,
            Latitude = latitude,
            Longitude = longitude,
            CameraMake = ParseText(fields, "exif-ifd0-Make"),
            CameraModel = ParseText(fields, "exif-ifd0-Model"),
            LensModel = ParseText(fields, "exif-ifd2-LensModel"),
            Software = ParseText(fields, "exif-ifd0-Software"),
            FocalLengthMm = ParseRational(fields, "exif-ifd2-FocalLength"),
            FocalLength35mm = ParsePositiveInt(fields, "exif-ifd2-FocalLengthIn35mmFilm"),
            FNumber = ParseRational(fields, "exif-ifd2-FNumber"),
            ExposureSeconds = ParseRational(fields, "exif-ifd2-ExposureTime"),
            Iso = ParsePositiveInt(fields, "exif-ifd2-ISOSpeedRatings"),
            RawJson = JsonSerializer.Serialize(fields, RawJsonOptions),
            ExtractorVersion = CaptureMetadataExtractor.CurrentVersion
        };
    }

    private static (DateTime? CapturedAt, short? OffsetMinutes) ParseCaptureTime(
        IReadOnlyDictionary<string, string> fields)
    {
        // IFD0 DateTime is left out on purpose: it records the last edit, not the capture.
        if (TryParseTime(fields, "Original", out var original, out var originalOffset))
            return (original, originalOffset);

        if (TryParseTime(fields, "Digitized", out var digitized, out var digitizedOffset))
            return (digitized, digitizedOffset);

        return (null, null);
    }

    private static bool TryParseTime(
        IReadOnlyDictionary<string, string> fields,
        string suffix,
        out DateTime? capturedAt,
        out short? offsetMinutes)
    {
        capturedAt = null;
        offsetMinutes = null;

        var parsed = DateTime.TryParseExact(
            fields.GetValueOrDefault($"exif-ifd2-DateTime{suffix}"),
            ExifDateFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var wallClock);

        if (!parsed || wallClock.Year < FirstPlausibleYear)
            return false;

        var withFraction = wallClock.Add(ParseFraction(fields.GetValueOrDefault($"exif-ifd2-SubSecTime{suffix}")));
        offsetMinutes = ParseOffsetMinutes(fields.GetValueOrDefault($"exif-ifd2-OffsetTime{suffix}"));

        var utc = withFraction.AddMinutes(-(offsetMinutes ?? 0));
        capturedAt = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

        return true;
    }

    private static TimeSpan ParseFraction(string? subSeconds)
    {
        if (string.IsNullOrEmpty(subSeconds) || !subSeconds.All(char.IsAsciiDigit))
            return TimeSpan.Zero;

        var digits = subSeconds.Length > MaxFractionDigits ? subSeconds[..MaxFractionDigits] : subSeconds;
        var ticks = long.Parse(digits.PadRight(MaxFractionDigits, '0'), CultureInfo.InvariantCulture);

        return TimeSpan.FromTicks(ticks);
    }

    private static short? ParseOffsetMinutes(string? offset)
    {
        if (offset is not { Length: 6 } || offset[3] != ':' || offset[0] is not ('+' or '-'))
            return null;

        var hoursParsed = int.TryParse(offset.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var hours);
        var minutesParsed = int.TryParse(offset.AsSpan(4, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes);

        if (!hoursParsed || !minutesParsed || minutes >= 60)
            return null;

        var total = hours * 60 + minutes;
        if (total > MaxOffsetMinutes)
            return null;

        return (short)(offset[0] == '-' ? -total : total);
    }

    private static (double? Latitude, double? Longitude) ParseLocation(IReadOnlyDictionary<string, string> fields)
    {
        var latitude = ParseCoordinate(fields, "exif-ifd3-GPSLatitude", "exif-ifd3-GPSLatitudeRef", "N", "S", 90);
        var longitude = ParseCoordinate(fields, "exif-ifd3-GPSLongitude", "exif-ifd3-GPSLongitudeRef", "E", "W", 180);

        if (latitude is null || longitude is null)
            return (null, null);

        // Devices without a fix can write zeros, and nobody's photo is really from that exact spot.
        if (latitude == 0 && longitude == 0)
            return (null, null);

        return (latitude, longitude);
    }

    private static double? ParseCoordinate(
        IReadOnlyDictionary<string, string> fields,
        string valueField,
        string referenceField,
        string positive,
        string negative,
        double limit)
    {
        // Without its reference the hemisphere is unknown, so a value would be a guess.
        var reference = fields.GetValueOrDefault(referenceField);
        if (reference != positive && reference != negative)
            return null;

        var parts = fields.GetValueOrDefault(valueField)?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts is not { Length: 3 })
            return null;

        var degrees = ParseRationalValue(parts[0]);
        var minutes = ParseRationalValue(parts[1]);
        var seconds = ParseRationalValue(parts[2]);

        if (degrees is null || minutes is null || seconds is null)
            return null;

        var magnitude = degrees.Value + minutes.Value / 60 + seconds.Value / 3600;
        if (magnitude > limit)
            return null;

        return reference == negative ? -magnitude : magnitude;
    }

    private static string? ParseText(IReadOnlyDictionary<string, string> fields, string name)
    {
        var value = fields.GetValueOrDefault(name);
        if (string.IsNullOrEmpty(value))
            return null;

        return value.Length > MaxTextLength ? value[..MaxTextLength] : value;
    }

    private static double? ParseRational(IReadOnlyDictionary<string, string> fields, string name)
    {
        var value = fields.GetValueOrDefault(name);
        return value is null ? null : ParseRationalValue(value);
    }

    private static double? ParseRationalValue(string value)
    {
        var parts = value.Split('/');
        if (parts.Length != 2)
            return null;

        var numeratorParsed = double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator);
        var denominatorParsed = double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator);

        if (!numeratorParsed || !denominatorParsed || denominator == 0 || numerator < 0 || denominator < 0)
            return null;

        return numerator / denominator;
    }

    private static int? ParsePositiveInt(IReadOnlyDictionary<string, string> fields, string name)
    {
        // Multi-value tags list one number per component, and the first is the one in use.
        var first = fields.GetValueOrDefault(name)?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (first is null)
            return null;

        var parsed = int.TryParse(first, NumberStyles.None, CultureInfo.InvariantCulture, out var value);

        // Zero is how these tags spell "unknown".
        return parsed && value > 0 ? value : null;
    }
}
