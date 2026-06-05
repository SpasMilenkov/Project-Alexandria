namespace Alexandria.Common.Policies;

/// <summary>
/// Mirrors the FileGroup union from the frontend mime utils.
/// Used as TriggerValue entries for FileGroup rules (semicolon-separated).
/// </summary>
public static class FileGroups
{
    public const string Images = "Images";
    public const string Videos = "Videos";
    public const string Audio = "Audio";
    public const string Documents = "Documents";
    public const string Spreadsheets = "Spreadsheets";
    public const string Presentations = "Presentations";
    public const string Archives = "Archives";
    public const string Code = "Code";
    public const string Text = "Text";

    private static readonly Dictionary<string, string[]> Prefixes = new()
    {
        [Images] = ["image/"],
        [Videos] = ["video/"],
        [Audio] = ["audio/"],
        [Documents] =
            ["application/pdf", "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml"],
        [Spreadsheets] =
            ["application/vnd.ms-excel", "application/vnd.openxmlformats-officedocument.spreadsheetml", "text/csv"],
        [Presentations] =
            ["application/vnd.ms-powerpoint", "application/vnd.openxmlformats-officedocument.presentationml"],
        [Archives] =
            ["application/zip", "application/x-rar", "application/x-7z", "application/x-tar", "application/gzip"],
        [Code] = ["text/javascript", "application/json", "text/html", "text/css", "text/x-python"],
        [Text] = ["text/plain", "text/markdown"],
    };

    public static bool MatchesMimeType(string groupsCsv, string mimeType)
    {
        var groups = groupsCsv.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var group in groups)
        {
            if (!Prefixes.TryGetValue(group, out var prefixes)) continue;
            if (prefixes.Any(p => mimeType.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }
}