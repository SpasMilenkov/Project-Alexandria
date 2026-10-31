namespace Alexandria.Common.Mime;

/// <summary>
/// A set of canonical MIME types plus top-level prefixes such as <c>image/</c>. Every backend MIME
/// rule and the generated frontend catalog use this one shape, so both sides match a type the
/// same way: exact type or listed prefix, nothing else.
/// </summary>
public sealed class MimeMatcher
{
    private readonly HashSet<string> _types;

    public MimeMatcher(IEnumerable<string>? prefixes = null, IEnumerable<string>? types = null)
    {
        Prefixes = [.. prefixes ?? []];
        Types = [.. types ?? []];
        _types = new HashSet<string>(Types, StringComparer.Ordinal);
    }

    /// <summary>Top-level prefixes, each ending in "/".</summary>
    public IReadOnlyList<string> Prefixes { get; }

    /// <summary>Exact canonical types, in declaration order.</summary>
    public IReadOnlyList<string> Types { get; }

    /// <summary>True when <paramref name="canonical"/> is listed or starts with a listed prefix.</summary>
    public bool Matches(string canonical)
    {
        if (_types.Contains(canonical)) return true;

        foreach (var prefix in Prefixes)
            if (canonical.StartsWith(prefix, StringComparison.Ordinal))
                return true;

        return false;
    }

    /// <summary>Merges matchers in order, dropping duplicates.</summary>
    public static MimeMatcher Union(IEnumerable<MimeMatcher> matchers)
    {
        var list = matchers.ToList();

        return new MimeMatcher(
            list.SelectMany(m => m.Prefixes).Distinct(StringComparer.Ordinal),
            list.SelectMany(m => m.Types).Distinct(StringComparer.Ordinal));
    }
}
