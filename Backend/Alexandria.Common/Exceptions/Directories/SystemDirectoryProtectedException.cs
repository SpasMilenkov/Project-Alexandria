using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Common.Exceptions.Directories;

/// <summary>
/// Raised when a request would trash, delete, move or rename a system folder such as Notes. Those
/// folders stay at the root of their owner's tree under their own name, because notes are found
/// by living under them.
/// </summary>
public class SystemDirectoryProtectedException : Exception
{
    public Guid DirectoryId { get; }
    public DirectorySystemKind Kind { get; }

    public SystemDirectoryProtectedException(Guid directoryId, DirectorySystemKind kind, string action)
        : base($"The {kind} folder can't be {action}.")
    {
        DirectoryId = directoryId;
        Kind = kind;
    }
}
