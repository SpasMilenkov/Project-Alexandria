using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Gallery;

/// <summary>
/// Turns a cursor into the opaque string clients send back. Opaque so clients never parse the
/// date: a JavaScript Date keeps milliseconds while Postgres stores microseconds, and a rounded
/// cursor would skip or repeat photos. The sort is included so a cursor from one ordering
/// cannot be replayed against the other.
/// </summary>
public static class GalleryCursorCodec
{
    private const char Separator = '.';

    public static string Encode(GalleryCursor cursor, GallerySort sort)
    {
        var raw = string.Create(
            CultureInfo.InvariantCulture,
            $"{(int)sort}{Separator}{cursor.SortDate.Ticks}{Separator}{cursor.FileId:N}");

        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(raw));
    }

    public static bool TryDecode(string? value, GallerySort sort, [NotNullWhen(true)] out GalleryCursor? cursor)
    {
        cursor = null;

        if (string.IsNullOrEmpty(value) || !Base64Url.IsValid(value))
            return false;

        var parts = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(value)).Split(Separator);
        if (parts.Length != 3)
            return false;

        var sortParsed = int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var sortValue);
        var ticksParsed = long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks);
        var idParsed = Guid.TryParseExact(parts[2], "N", out var fileId);

        if (!sortParsed || !ticksParsed || !idParsed || sortValue != (int)sort)
            return false;

        if (ticks > DateTime.MaxValue.Ticks)
            return false;

        cursor = new GalleryCursor { SortDate = new DateTime(ticks, DateTimeKind.Utc), FileId = fileId };

        return true;
    }
}