namespace Common.Services;

public interface IImagePreviewService
{
    public Task<Stream> GenerateImagePreview(Stream imageTopPreview, string? format, int width = 1280, int height = 720);
    
}