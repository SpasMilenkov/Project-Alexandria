namespace Alexandria.Dto.Files;

/// <summary>Either the upload's ID and signed URL, or the reason it was rejected.</summary>
public sealed record UploadInitResult(
    Guid? UploadId,
    string? UploadUrl,
    string? Error);
