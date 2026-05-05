using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Preview.Media.Dto;

namespace Alexandria.Services.Preview.Media;

public interface IMediaPreviewService
{
    public Task<MediaPreviewResult?> GeneratePreviewAsync(string inputPath, FileCategory fileCategory,
        CancellationToken ct);
}