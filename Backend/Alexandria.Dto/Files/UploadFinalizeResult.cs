namespace Alexandria.Dto.Files;

public sealed record UploadFinalizeResult(
    Guid UploadId,
    bool Success,
    string? Error);
