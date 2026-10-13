using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Preview.Media.Dto;
using Alexandria.Dto.Files;

namespace Alexandria.Services.Preview.Media;

public interface IMediaPreviewService
{
    Task<MediaMetadataDto> ExtractMetadataAsync(string inputPath, CancellationToken ct);

    public Task<MediaPreviewResult?> GeneratePreviewAsync(string inputPath, FileCategory fileCategory,
        CancellationToken ct);
}
