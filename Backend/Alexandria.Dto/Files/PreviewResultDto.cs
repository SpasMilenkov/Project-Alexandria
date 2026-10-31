namespace Alexandria.Dto.Files;

public record PreviewResultDto(
    string? PreviewUrl,
    TextPreviewDto? TextPreview = null,
    string? ArchivePreview = null);