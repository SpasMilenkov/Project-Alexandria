using System.Text;
using Alexandria.Common.Validation;

namespace Alexandria.Services.Storage.Notes;

/// <summary>
/// Turns a typed title into a markdown file name that is valid here and on the file systems a
/// vault export lands on.
/// </summary>
internal static class NoteNames
{
    public const string Extension = ".md";
    public const string DefaultTitle = "Untitled";

    // Room for the extension and a " 999" suffix inside the folder name limit files share.
    private const int MaxTitleLength = DirectoryNameRules.MaxLength - 8;

    /// <summary>
    /// The title with characters names can't hold replaced by spaces, a typed ".md" dropped and
    /// whitespace collapsed. Falls back to <see cref="DefaultTitle"/> when nothing usable is left.
    /// </summary>
    public static string CleanTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return DefaultTitle;

        var builder = new StringBuilder(title.Length);
        var lastWasSpace = false;

        foreach (var c in title.Trim())
        {
            var isSpace = char.IsWhiteSpace(c) || char.IsControl(c) || DirectoryNameRules.InvalidCharacters.Contains(c);

            if (isSpace && lastWasSpace) continue;

            builder.Append(isSpace ? ' ' : c);
            lastWasSpace = isSpace;
        }

        var cleaned = builder.ToString().Trim();

        if (cleaned.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[..^Extension.Length].TrimEnd();

        // Windows drops trailing dots, so "Plans." would export as a different name.
        cleaned = cleaned.TrimEnd('.', ' ');

        if (cleaned.Length > MaxTitleLength)
            cleaned = cleaned[..MaxTitleLength].TrimEnd('.', ' ');

        return cleaned.Length == 0 ? DefaultTitle : cleaned;
    }

    /// <summary>
    /// "Title.md", or "Title 2.md" and up when taken. Names compare ignoring case, because an
    /// exported vault can land on a case-insensitive file system.
    /// </summary>
    public static string FirstFreeName(string cleanTitle, IReadOnlySet<string> takenNames,
        string extension = Extension)
    {
        var candidate = cleanTitle + extension;

        for (var counter = 2; Contains(takenNames, candidate); counter++)
            candidate = $"{cleanTitle} {counter}{extension}";

        return candidate;
    }

    /// <summary>
    /// "Untitled.md", "Untitled 2.md" and so on: the names "New note" hands out, which the note's
    /// first line replaces. Any other name counts as chosen.
    /// </summary>
    public static bool IsUntitled(string fileName)
    {
        var title = Path.GetFileNameWithoutExtension(fileName);

        if (title.Equals(DefaultTitle, StringComparison.OrdinalIgnoreCase)) return true;
        if (!title.StartsWith(DefaultTitle + " ", StringComparison.OrdinalIgnoreCase)) return false;

        return int.TryParse(title.AsSpan(DefaultTitle.Length + 1), out var counter) && counter >= 2;
    }

    /// <summary>
    /// The extension a rename keeps, so a note stays markdown whatever it is called. ".md" for a
    /// name without one.
    /// </summary>
    public static string ExtensionOf(string fileName)
    {
        var extension = Path.GetExtension(fileName);

        return extension.Length == 0 ? Extension : extension;
    }

    private static bool Contains(IReadOnlySet<string> takenNames, string candidate) =>
        takenNames.Any(name => string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase));
}
