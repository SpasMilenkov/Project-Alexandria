using Alexandria.Data.Models;

namespace Alexandria.Dto.SignedUrls;

public sealed class SharedFileMetadataDto
{
    public required string FileName { get; init; }
    public required string MimeType { get; init; }
    public required long Size { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required int VersionNumber { get; init; }

    /// <summary>True when the link targets a specific pinned version rather than always-current.</summary>
    public required bool IsPinnedVersion { get; init; }

    /// <summary>
    /// True when the shared bytes are client-side ciphertext. MimeType still names the plaintext
    /// type, so viewers must check this before rendering the content inline.
    /// </summary>
    public required bool IsEncrypted { get; init; }

    public static SharedFileMetadataDto FromEntities(SignedUrl signedUrl, FileVersion version) => new()
    {
        FileName = signedUrl.FileInfo.Name,
        MimeType = version.MimeType,
        Size = version.Size,
        ExpiresAt = signedUrl.ExpiresAt,
        VersionNumber = version.VersionNumber,
        IsPinnedVersion = signedUrl.FileVersionId.HasValue,
        IsEncrypted = version.IsEncrypted,
    };
}