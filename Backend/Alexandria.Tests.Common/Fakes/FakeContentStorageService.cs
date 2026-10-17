using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Metrics;

namespace Alexandria.Tests.Common.Fakes;

public class FakeContentStorageService : IContentStorageService
{
    private readonly Dictionary<string, Dictionary<string, byte[]>> _store = new();

    public List<(string Bucket, string Key, byte[] Bytes)> UploadedObjects { get; } = [];
    public Func<Guid>? UploadIdFactory { get; set; } = null;

    public void Seed(string bucket, string key, byte[] bytes)
    {
        if (!_store.TryGetValue(bucket, out var bucketStore))
        {
            bucketStore = [];
            _store[bucket] = bucketStore;
        }

        bucketStore[key] = bytes;
    }

    public void Reset()
    {
        _store.Clear();
        UploadedObjects.Clear();
        UploadIdFactory = null;
    }

    public Task<Stream> DownloadFile(Guid versionId, Guid userId, CancellationToken ct = default)
    {
        var key = versionId.ToString();
        if (_store.TryGetValue("alexandria-files", out var bucket) && bucket.TryGetValue(key, out var bytes))
            return Task.FromResult<Stream>(new MemoryStream(bytes));

        throw new InvalidOperationException($"File not found: {versionId}");
    }

    public Task<Stream> DownloadSeekableFile(Guid versionId, Guid userId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> DownloadSeekableFile(Guid versionId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task DownloadContentObjectAsync(Guid contentObjectId, string localFilePath, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task UploadStreamingOutputAsync(string localDirectory, string keyPrefix, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task DeleteStreamingOutputByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetStreamManifest(Guid versionId, Guid userId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<DownloadInfo> GetFileDownloadDetails(Guid fileId, Guid userId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<DownloadInfo> GetFilVersioneDownloadDetails(Guid versionId, Guid userId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task StreamFile(Guid versionId, Stream destination, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public FileCategory CategorizeFile(string mimeType) => FileCategory.Unknown;

    public Task<string> GetFilePresignedUrl(Guid fileId, byte[] hash, string fileName, TimeSpan expiry)
        => Task.FromResult($"http://fake-s3/files/{fileId}/{fileName}");

    public Task<UploadResult> FinalizeFileUpload(string objectName, Guid uploadId, Guid uploadedBy,
        byte[]? encryptionIv, byte[]? encryptionSalt,
        byte[]? integrityTag, string? encryptionHint, int? iterationCount, bool isEncrypted = false,
        Guid? directoryId = null, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<(Guid, string)> InitiateFileUpload(
        string contentType,
        string clientHash,
        Guid userId,
        long contentLength,
        Guid? directoryId = null,
        CancellationToken ct = default)
    {
        var uploadId = UploadIdFactory?.Invoke() ?? Guid.NewGuid();
        var uploadUrl = $"http://fake-s3/upload/{Guid.NewGuid()}";
        return Task.FromResult((uploadId, uploadUrl));
    }

    public Task<StorageBreakdown> GetStorageBreakdown(Guid userId, bool summary = false, CancellationToken ct = default)
        => Task.FromResult(new StorageBreakdown
        {
            SizeByType = new Dictionary<string, long>(),
            TrashSize = 0,
            OldFiles = []
        });

    public Task<string> GetVersionPresignedUrl(Guid fileVersionId, Guid ownerId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task StreamBulkZipAsync(Guid[] directoryIds, Guid[] fileIds, Guid userId, Stream destination,
        CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}