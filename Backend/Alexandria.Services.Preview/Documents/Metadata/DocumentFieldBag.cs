namespace Alexandria.Services.Preview.Documents.Metadata;

/// <summary>
/// Collects reader output with bounds, because every value comes from an uploaded file and ends
/// up in a database row.
/// </summary>
internal sealed class DocumentFieldBag
{
    internal const int MaxFields = 300;
    internal const int MaxValueLength = 4000;

    internal const string ListSeparator = "; ";

    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> Values => _values;

    public bool Contains(string key) => _values.ContainsKey(key);

    /// <summary>
    /// Adds the value, or appends it to an existing one for keys a format may repeat, such as
    /// one keyword per element.
    /// </summary>
    public void Add(string key, string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return;

        if (_values.TryGetValue(key, out var existing))
        {
            if (existing == trimmed || existing.Length >= MaxValueLength)
                return;

            _values[key] = Cap(existing + ListSeparator + trimmed);
            return;
        }

        if (_values.Count >= MaxFields)
            return;

        _values[key] = Cap(trimmed);
    }

    public DocumentFields ToFields() => new(_values, IsPasswordProtected: false);

    private static string Cap(string value) => value.Length > MaxValueLength ? value[..MaxValueLength] : value;
}
