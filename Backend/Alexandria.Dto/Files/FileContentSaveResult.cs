namespace Alexandria.Dto.Files;

/// <summary>What a text save or create left behind, enough for the editor to keep saving.</summary>
public sealed record FileContentSaveResult
{
    public required Guid FileId { get; init; }
    public required Guid VersionId { get; init; }
    public required int VersionNumber { get; init; }
    public required long Size { get; init; }
    public required string FileName { get; init; }
    public required string MimeType { get; init; }
    public required Guid? DirectoryId { get; init; }

    /// <summary>A new file was created rather than a version added to one.</summary>
    public required bool Created { get; init; }

    /// <summary>The text matched the parent version byte for byte, so nothing was written.</summary>
    public required bool Unchanged { get; init; }
}
