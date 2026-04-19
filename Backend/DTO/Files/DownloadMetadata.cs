namespace DTO.Files;

public sealed record DownloadMetadata
{
    public required string FileName { get; init; }
    public required string MimeType { get; init; }
    public required bool IsEncrypted { get; init; }
    public byte[]? EncryptionIv { get; init; }
    public byte[]? EncryptionSalt { get; init; }
    public byte[]? IntegrityTag { get; init; }
    public string? EncryptionHint { get; init; }
}
