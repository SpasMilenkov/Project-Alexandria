namespace Alexandria.Data.Audit;

internal static class AuditSqlClassifier
{
    /// <summary>
    /// True for statements that start with SELECT once leading whitespace and "--" comment lines
    /// (from TagWith) are skipped. Anything else, CTEs included, is treated as a possible write.
    /// </summary>
    public static bool IsRead(string? sql)
    {
        if (string.IsNullOrEmpty(sql))
            return true;

        var text = sql.AsSpan();

        while (true)
        {
            text = text.TrimStart();

            if (!text.StartsWith("--"))
                break;

            var lineEnd = text.IndexOf('\n');
            if (lineEnd < 0)
                return true;

            text = text[(lineEnd + 1)..];
        }

        return text.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase);
    }
}
