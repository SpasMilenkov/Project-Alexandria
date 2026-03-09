namespace DTO.Files;

public record VersionDownloadInfo(string FileName, string MimeType, int VersionNumber, byte[] Hash);
