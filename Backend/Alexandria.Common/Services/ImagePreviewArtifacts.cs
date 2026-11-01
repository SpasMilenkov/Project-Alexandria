namespace Alexandria.Common.Services;

public sealed record ImagePreviewArtifacts(ImagePreviewResult Preview, ImagePreviewResult Thumbnail)
    : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        await Preview.DisposeAsync();
        await Thumbnail.DisposeAsync();
    }
}
