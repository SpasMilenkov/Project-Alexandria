using Alexandria.Common.Services;

namespace Alexandria.Tests.Common.Fakes;

public class FakeStaticAssetService : IStaticAssetService
{
    public List<string> DeletedKeys { get; } = [];

    public void Reset()
    {
        DeletedKeys.Clear();
    }

    public Task<string> UploadBackgroundImageAsync(Stream content, Guid userId,
        CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task DeleteBackgroundImageAsync(string objectKey, CancellationToken ct = default)
    {
        DeletedKeys.Add(objectKey);
        return Task.CompletedTask;
    }

    public Task<string> UploadPlaylistCoverAsync(Stream content, Guid playlistId,
        Guid userId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
