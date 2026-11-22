using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Links;

/// <summary>
/// What a note shows for one link: the target's live name and whether it can be opened. Only
/// <see cref="Kind"/>, <see cref="Id"/> and <see cref="State"/> are set for an unavailable target.
/// </summary>
public sealed record LinkTargetDto
{
    public required LinkTargetKind Kind { get; init; }
    public required Guid Id { get; init; }
    public required LinkTargetState State { get; init; }
    public string? Name { get; init; }

    /// <summary>Files only: the current version's type, which decides where the link opens.</summary>
    public string? MimeType { get; init; }

    /// <summary>Files only, null at the root: the folder "show in folder" opens.</summary>
    public Guid? DirectoryId { get; init; }

    /// <summary>Files only: the current version is owner-encrypted, so the editor can't open it.</summary>
    public bool IsEncrypted { get; init; }

    /// <summary>Tags only: the tag's colour, for the swatch next to its name.</summary>
    public string? Color { get; init; }
}
