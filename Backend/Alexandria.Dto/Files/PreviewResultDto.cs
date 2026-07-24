namespace Alexandria.Dto.Files;

public record PreviewResultDto(
    string? PreviewUrl,
    string? TextPreview = null,
    string? ArchivePreview = null);