namespace Alexandria.Dto.Files;

public record BulkDownloadEntry(
    Guid FileId,
    string FileName,
    string DirectoryPath,
    string StorageKey,
    bool IsPromoted, // determines which bucket to hit
    string? TempObjectKey, // non-null when !IsPromoted
    bool IsEncrypted,
    byte[]? EncryptionIv,
    byte[]? EncryptionSalt,
    byte[]? IntegrityTag,
    string? EncryptionHint,
    int? IterationCount
);