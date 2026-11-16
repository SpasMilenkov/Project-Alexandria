using System.Buffers;

namespace Alexandria.Common.Validation;

/// <summary>
/// Folder name limits shared by folder creation, folder uploads and request validators, so a name
/// accepted at the edge is never rejected later by the service.
/// </summary>
public static class DirectoryNameRules
{
    public const int MaxLength = 255;

    public const string InvalidCharacters = "/\\:*?\"<>|";

    private static readonly SearchValues<char> InvalidCharacterSet = SearchValues.Create(InvalidCharacters);

    /// <summary>Returns why <paramref name="name"/> is not a valid folder name, or null when it is.</summary>
    public static string? FindProblem(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Folder name cannot be empty.";
        }

        if (name.Length > MaxLength)
        {
            return $"Folder name cannot exceed {MaxLength} characters.";
        }

        if (name.AsSpan().IndexOfAny(InvalidCharacterSet) >= 0)
        {
            return $"Folder name cannot contain any of {string.Join(" ", InvalidCharacters.ToCharArray())}";
        }

        return null;
    }
}
