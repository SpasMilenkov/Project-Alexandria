namespace Alexandria.Services.Storage.Content.Mime;

/// <summary>
/// Groups of container formats that share magic bytes. Inside a family the bytes cannot reliably
/// tell members apart, so the file extension breaks ties. All entries are canonical types.
/// </summary>
internal static class MimeFamilies
{
    private const string ZipBased = "zip-based";
    private const string IsoBmff = "iso-bmff";
    private const string Matroska = "matroska";
    private const string Ogg = "ogg";

    private static readonly Dictionary<string, string> FamilyByMime = Build(
        (ZipBased,
        [
            "application/zip",
            "application/java-archive",
            "application/epub+zip",
            "application/vnd.android.package-archive",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "application/vnd.oasis.opendocument.text",
            "application/vnd.oasis.opendocument.spreadsheet",
            "application/vnd.oasis.opendocument.presentation"
        ]),
        (IsoBmff, ["video/mp4", "audio/mp4", "video/x-m4v", "video/quicktime"]),
        (Matroska, ["video/webm", "audio/webm", "video/x-matroska", "audio/x-matroska"]),
        (Ogg, ["audio/ogg", "video/ogg", "application/ogg"]));

    public static string? FamilyOf(string canonical)
        => FamilyByMime.GetValueOrDefault(canonical);

    public static bool SameFamily(string a, string b)
    {
        var family = FamilyOf(a);
        return family is not null && string.Equals(family, FamilyOf(b), StringComparison.Ordinal);
    }

    public static bool IsZipBased(string canonical)
        => string.Equals(FamilyOf(canonical), ZipBased, StringComparison.Ordinal);

    internal static IEnumerable<string> AllTypes => FamilyByMime.Keys;

    private static Dictionary<string, string> Build(params (string Family, string[] Types)[] groups)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (family, types) in groups)
        foreach (var type in types)
            map.Add(type, family);
        return map;
    }
}
