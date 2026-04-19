using System;

namespace DTO.Files;

public sealed record DownloadInfo
{
    public string PresignedUrl { get; init; } = null!;
    public string FileName { get; init; } = null!;
    public string MimeType { get; init; } = null!;
    public bool IsEncrypted { get; init; }
    public byte[]? EncryptionIv { get; init; }
    public byte[]? EncryptionSalt { get; init; }
    public byte[]? IntegrityTag { get; init; }
    public string? EncryptionHint { get; init; }
}
