namespace Alexandria.Dto.Files;

public record UploadResult(
    string ObjectName,
    long Size,
    Guid FileId,
    string MimeType,
    Guid VersionId,
    bool IsNewVersion,
    bool isEncrypted);