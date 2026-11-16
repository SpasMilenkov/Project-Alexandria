using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Common.Library;

/// <summary>
/// A starter folder created when a user finishes onboarding. The groups name the file groups its
/// automation targets; a null group means the folder never gets that kind of rule.
/// </summary>
public sealed record LibraryFolderPreset
{
    public required LibraryFolderKind Kind { get; init; }
    public required string DefaultName { get; init; }
    public string? TranscodeGroup { get; init; }
    public string? AutoTagGroup { get; init; }
}
