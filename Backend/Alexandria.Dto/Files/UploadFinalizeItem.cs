namespace Alexandria.Dto.Files;

public sealed record UploadFinalizeItem
{
    public required Guid UploadId { get; init; }
    public required Guid? DirectoryId { get; init; }
    public required string FileName { get; init; }
    public required bool IsEncrypted { get; init; }
    public required byte[]? EncryptionIv { get; init; }
    public required byte[]? EncryptionSalt { get; init; }
    public required byte[]? IntegrityTag { get; init; }
    public required string? EncryptionHint { get; init; }
    public required int? IterationCount { get; init; }
}
