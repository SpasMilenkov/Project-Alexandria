namespace Alexandria.Dto.Files;

public record PreviewResultDto(
    FileSummary MetaData,
    string? PreviewUrl,
    string? ThumbnailUrl,
    string? TextPreview = null,
    string? ArchivePreview = null);