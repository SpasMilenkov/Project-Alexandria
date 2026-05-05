namespace Alexandria.Dto.Files;

public record VersionDownloadInfo(string FileName, string MimeType, int VersionNumber, byte[] Hash);