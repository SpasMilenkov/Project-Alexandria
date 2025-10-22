using Models.Enumerators;
using PreviewService.Media.Dto;

namespace PreviewService.Media;

public interface IMediaPreviewService
{
    public Task<MediaPreviewResult?> GeneratePreviewAsync(string inputPath, FileCategory fileCategory,
        CancellationToken ct);

}