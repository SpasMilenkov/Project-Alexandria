using System.Reflection;

namespace Alexandria.Data.Sql;

/// <summary>
/// Reads SQL scripts embedded from the Sql folder. Each migration owns its own subfolder, so a
/// later change to a trigger is a new file and an applied migration never changes underneath.
/// </summary>
public static class EmbeddedSql
{
    private static readonly Assembly Assembly = typeof(EmbeddedSql).Assembly;

    /// <summary>All scripts under <paramref name="folder"/> (e.g. "Audit.ExpandAuditLogging"), in name order.</summary>
    public static IReadOnlyList<string> ReadFolder(string folder)
    {
        var prefix = $"{Assembly.GetName().Name}.Sql.{folder}.";

        var names = Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix, StringComparison.Ordinal)
                        && n.EndsWith(".sql", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        if (names.Count == 0)
            throw new InvalidOperationException($"No embedded SQL scripts found under {prefix}");

        return names.Select(Read).ToList();
    }

    private static string Read(string resourceName)
    {
        using var stream = Assembly.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException($"Missing embedded SQL script {resourceName}");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
