namespace Alexandria.Common.Services;

/// <summary>
/// One encoded preview artifact and the MIME type it was actually encoded as, which can differ
/// from the source MIME type.
/// </summary>
public sealed record ImagePreviewResult(Stream Content, string ContentType) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
