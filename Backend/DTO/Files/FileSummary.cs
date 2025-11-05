namespace DTO.Files;

public record FileSummary(string FileName, string MimeType, bool HasPreview, string Path);