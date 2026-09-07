namespace Alexandria.Dto.StorageStats;

public sealed record UserStorageRankDto(
    Guid UserId,
    string UserName,
    long FilesSize,
    long PreviewsSize,
    long TranscodedSize,
    long UsedBytes,
    long QuotaBytes);