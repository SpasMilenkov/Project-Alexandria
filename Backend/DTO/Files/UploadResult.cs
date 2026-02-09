namespace DTO.Files;

public record UploadResult(string ObjectName, string Checksum, long Size, Guid FileId);