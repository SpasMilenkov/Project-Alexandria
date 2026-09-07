namespace Alexandria.Dto.StorageStats;

public sealed record StorageSplitResponse(
    long FilesSize,
    long PreviewsSize,
    long TranscodedSize,
    long TrashSize);