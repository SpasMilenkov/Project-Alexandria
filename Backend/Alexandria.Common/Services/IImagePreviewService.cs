namespace Alexandria.Common.Services;

public interface IImagePreviewService
{
    public Task<Stream> GenerateImagePreviewAsync(Stream imageToPreview, string? format, int width = 1280,
        int height = 720, CancellationToken ct = default);

    Task<Stream> GenerateImageThumbnailAsync(Stream imageToPreview, string? format, int size = 320,
        CancellationToken ct = default);
}