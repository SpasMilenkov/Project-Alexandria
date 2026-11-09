namespace Alexandria.Services.Preview.Documents.Metadata;

/// <summary>
/// What a reader found, keyed by where it came from (<c>core:title</c>, <c>pdf:Author</c>,
/// <c>xmp:dc:language</c>...). Values are the file's own text, untouched apart from trimming.
/// </summary>
internal sealed record DocumentFields(IReadOnlyDictionary<string, string> Values, bool IsPasswordProtected)
{
    public static DocumentFields PasswordProtected { get; } =
        new(new Dictionary<string, string>(StringComparer.Ordinal), IsPasswordProtected: true);
}
