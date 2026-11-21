namespace Alexandria.Data.Models.Enumerators;

/// <summary>
/// A folder the app relies on by identity rather than by name. Each owner has at most one of each
/// kind, and it is never renamed, trashed, moved or deleted. Ordinary folders have none.
/// </summary>
public enum DirectorySystemKind
{
    Notes = 1,
}
