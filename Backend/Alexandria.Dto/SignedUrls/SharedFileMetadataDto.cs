using Alexandria.Data.Models;

namespace Alexandria.Dto.SignedUrls;

public sealed class SharedFileMetadataDto
{
    public required string FileName { get; init; }
    public required string MimeType { get; init; }
    public required long Size { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required bool HasPreview { get; init; }
    public required int VersionNumber { get; init; }

    /// <summary>True when the link targets a specific pinned version rather than always-current.</summary>
    public required bool IsPinnedVersion { get; init; }

    public static SharedFileMetadataDto FromEntities(SignedUrl signedUrl, FileVersion version) => new()
    {
        FileName = signedUrl.FileInfo.Name,
        MimeType = version.MimeType,
        Size = version.Size,
        ExpiresAt = signedUrl.ExpiresAt,
        HasPreview = signedUrl.FileInfo.HasPreview,
        VersionNumber = version.VersionNumber,
        IsPinnedVersion = signedUrl.FileVersionId.HasValue,
    };
}