namespace DTO.Files;

public record UploadResult(string ObjectName, string Checksum, Guid VersionId, long Size, Guid FileId);