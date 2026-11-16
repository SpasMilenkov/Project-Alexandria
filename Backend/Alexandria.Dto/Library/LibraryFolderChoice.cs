using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Library;

/// <summary>
/// What the user picked for one starter folder. Rung lists and the auto-tag flag only matter for
/// folders whose preset supports them; the server drops anything this installation cannot run.
/// </summary>
public sealed record LibraryFolderChoice
{
    public LibraryFolderKind Kind { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool AutomationEnabled { get; init; }
    public AudioRung[] AudioRungs { get; init; } = [];
    public VideoRung[] VideoRungs { get; init; } = [];
    public bool AutoTag { get; init; }
}
