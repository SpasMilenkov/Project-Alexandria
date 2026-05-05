namespace Alexandria.Dto.Files;

public record FileSummary(Guid Id, string FileName, string MimeType, bool HasPreview);