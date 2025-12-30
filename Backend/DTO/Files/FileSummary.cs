namespace DTO.Files;

public record FileSummary(Guid Id, string FileName, string MimeType, bool HasPreview);