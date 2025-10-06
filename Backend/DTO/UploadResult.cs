namespace DTO;

public record UploadResult(string ObjectName, string Url, string Checksum, string VersionId, long Size, Guid FileId);