using System.Buffers.Binary;
using System.Text;

namespace Alexandria.Services.Storage.Content.Mime;

/// <summary>
/// Reads the brands of a leading ISO BMFF <c>ftyp</c> box. MimeDetective's default definitions
/// match only the generic box signature, so every member of the family sniffs as
/// <c>video/mp4</c>. The brands are the only bytes that tell an AVIF image apart from a video.
/// </summary>
internal static class IsoBmffBrands
{
    private const int BrandLength = 4;
    private const int MajorBrandOffset = 8;
    private const int CompatibleBrandsOffset = 16;

    private static readonly Dictionary<string, string> MimeByBrand = new(StringComparer.Ordinal)
    {
        ["avif"] = "image/avif",
        ["avis"] = "image/avif"
    };

    internal static IEnumerable<string> AllTypes => MimeByBrand.Values.Distinct(StringComparer.Ordinal);

    /// <summary>
    /// The type named by the major brand, else by the first known compatible brand. Null when the
    /// header does not start with an <c>ftyp</c> box or names no brand this map knows.
    /// </summary>
    public static string? Detect(byte[] header)
    {
        if (header.Length < CompatibleBrandsOffset) return null;
        if (!header.AsSpan(BrandLength, BrandLength).SequenceEqual("ftyp"u8)) return null;

        if (TryMapBrand(header, MajorBrandOffset, out var mime)) return mime;

        // A size below the fixed fields (0 means "to end of file", 1 a 64-bit size) carries no
        // usable bound, so only the major brand is trusted then.
        var boxSize = BinaryPrimitives.ReadUInt32BigEndian(header);
        if (boxSize < CompatibleBrandsOffset) return null;

        var end = (int)Math.Min(boxSize, (uint)header.Length);

        for (var offset = CompatibleBrandsOffset; offset + BrandLength <= end; offset += BrandLength)
        {
            if (TryMapBrand(header, offset, out mime)) return mime;
        }

        return null;
    }

    private static bool TryMapBrand(byte[] header, int offset, out string? mime)
        => MimeByBrand.TryGetValue(Encoding.ASCII.GetString(header, offset, BrandLength), out mime);
}
