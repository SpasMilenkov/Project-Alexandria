using System.Globalization;
using System.Text.RegularExpressions;
using NetVips;

namespace Alexandria.Services.Preview.Images;

internal static partial class CaptureHeaderReader
{
    private const string ExifPrefix = "exif-ifd";
    private const string AsciiFormat = "ASCII";
    private const string DescriptionSeparator = " (";

    // Opaque vendor bytes that libvips only reports as "1602 bytes undefined data".
    private const string MakerNoteSuffix = "-MakerNote";

    private static readonly string[] FormatFields =
    [
        "vips-loader",
        "heif-compression",
        "bits-per-sample",
        "n-pages",
        "jpeg-multiscan",
        "jpeg-chroma-subsample"
    ];

    /// <summary>
    /// Reads the header without decoding pixels. A format with no libvips loader yields an empty
    /// header. A loader that fails throws <see cref="VipsException"/>, which can also mean a
    /// storage read failed, so callers must not treat it as "this file has no metadata".
    /// </summary>
    public static CaptureHeader Read(Stream source)
    {
        source.Position = 0;
        var loader = Image.FindLoadStream(source);
        source.Position = 0;

        if (loader is null)
            return new CaptureHeader();

        using var image = Image.NewFromStream(source, access: Enums.Access.Sequential);

        return new CaptureHeader
        {
            Width = image.Width,
            Height = image.PageHeight,
            Orientation = image.Contains("orientation") ? (int)image.Get("orientation") : null,
            Fields = ReadFields(image)
        };
    }

    private static Dictionary<string, string> ReadFields(Image image)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var name in image.GetFields())
        {
            if (!IsKept(name))
                continue;

            var value = Convert.ToString(image.Get(name), CultureInfo.InvariantCulture);
            if (value is null)
                continue;

            var raw = name.StartsWith(ExifPrefix, StringComparison.Ordinal) ? RawExifValue(value) : value;
            var clean = Sanitize(raw);

            if (clean.Length > 0)
                fields[name] = clean;
        }

        return fields;
    }

    private static bool IsKept(string name)
    {
        if (name.StartsWith(ExifPrefix, StringComparison.Ordinal))
            return !name.EndsWith(MakerNoteSuffix, StringComparison.Ordinal);

        return FormatFields.Contains(name, StringComparer.Ordinal);
    }

    /// <summary>
    /// libvips formats EXIF values as <c>raw (description, Format, n components, m bytes)</c>.
    /// Only the raw part is reliable: the description of enum tags can read
    /// "Internal error (unknown value 6)". Text can contain parentheses itself, so text values
    /// are recovered from the fact that their description repeats them exactly.
    /// </summary>
    internal static string RawExifValue(string formatted)
    {
        var suffix = ExifSuffix().Match(formatted);
        if (!suffix.Success)
            return formatted;

        var body = formatted[..suffix.Index];

        if (suffix.Groups["format"].Value == AsciiFormat)
        {
            var length = (body.Length - DescriptionSeparator.Length) / 2;
            var isMirrored = length >= 0
                             && body.Length == length * 2 + DescriptionSeparator.Length
                             && string.CompareOrdinal(body, 0, body, length + DescriptionSeparator.Length, length) == 0;

            if (isMirrored)
                return body[..length];
        }

        var separator = body.IndexOf(DescriptionSeparator, StringComparison.Ordinal);

        return separator < 0 ? body : body[..separator];
    }

    // Postgres rejects NUL characters in both text and jsonb, and cameras pad fields with them.
    private static string Sanitize(string value) => value.Replace("\0", string.Empty).Trim();

    [GeneratedRegex(@", (?<format>[A-Za-z]+), \d+ components, \d+ bytes\)$", RegexOptions.Singleline)]
    private static partial Regex ExifSuffix();
}
