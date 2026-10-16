using Alexandria.Common.Services;

namespace Alexandria.Tests.Common.Fakes;

public class FakeStaticAssetService : IStaticAssetService
{
    public List<string> DeletedKeys { get; } = [];

    public void Reset()
    {
        DeletedKeys.Clear();
    }

    public Task<string> GenerateImageUploadUrl(string objectKey, TimeSpan expiry)
        => Task.FromResult($"http://fake-s3/upload/{objectKey}");

    public Task<string> GenerateBackgroundImageGetUrl(string objectKey, TimeSpan expiry)
        => Task.FromResult($"http://fake-s3/bg/{objectKey}");

    public Task DeleteBackgroundImageAsync(string objectKey, CancellationToken ct = default)
    {
        DeletedKeys.Add(objectKey);
        return Task.CompletedTask;
    }

    public Task<string> GetPlaylistCoverUploadUrlAsync(Guid playlistId, Guid userId, string contentType,
        CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}