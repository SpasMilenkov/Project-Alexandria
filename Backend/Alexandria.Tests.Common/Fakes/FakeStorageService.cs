using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Metrics;

namespace Alexandria.Tests.Common.Fakes;

public class FakeStorageService : IStorageService
{
    private readonly Dictionary<string, Dictionary<string, byte[]>> _store = new();

    public List<(string Bucket, string Key, byte[] Bytes)> UploadedObjects { get; } = [];
    public List<string> DeletedKeys { get; } = [];

    public PreviewResultDto? PreviewOverride { get; set; } = null;
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
        DeletedKeys.Clear();
        PreviewOverride = null;
        UploadIdFactory = null;
    }

    // public Task UploadPreview(string objectName,
    //     string contentType,
    //     Stream fileStream,
    //     Guid originalFileId,
    //     Guid uploadedBy,
    //     long contentLength = -1L,
    //     string? originalFileName = null,
    //     CancellationToken ct = default)
    // {
    //     using var ms = new MemoryStream();
    //     fileStream.CopyTo(ms);
    //     var bytes = ms.ToArray();
    //     const string bucket = "alexandria-previews";
    //
    //     if (!_store.TryGetValue(bucket, out var bucketStore))
    //     {
    //         bucketStore = new Dictionary<string, byte[]>();
    //         _store[bucket] = bucketStore;
    //     }
    //
    //     bucketStore[objectName] = bytes;
    //
    //     UploadedObjects.Add((bucket, objectName, bytes));
    //
    //     var checksum = Convert.ToHexString(SHA256.HashData(bytes));
    //     return Task.FromResult(new UploadResult(objectName, checksum, bytes.Length, originalFileId));
    // }

    public Task UploadPreview(string objectName, string contentType, Stream fileStream, Guid originalFileId,
        Guid uploadedBy,
        long contentLength = -1, string? originalFileName = null, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public async Task UploadMediaData(
        Stream previewStream,
        Stream thumbnailStream,
        string objectName,
        Guid fileId,
        MediaMetadataDto metadataDto,
        CancellationToken ct = default)
    {
        const string bucket = "alexandria-previews";
        if (!_store.TryGetValue(bucket, out var bucketStore))
        {
            bucketStore = new Dictionary<string, byte[]>();
            _store[bucket] = bucketStore;
        }

        using var previewMs = new MemoryStream();
        await previewStream.CopyToAsync(previewMs, ct);
        var previewBytes = previewMs.ToArray();
        bucketStore[$"{objectName}-preview"] = previewBytes;
        UploadedObjects.Add((bucket, $"{objectName}-preview", previewBytes));

        using var thumbMs = new MemoryStream();
        await thumbnailStream.CopyToAsync(thumbMs, ct);
        var thumbBytes = thumbMs.ToArray();
        bucketStore[$"{objectName}-thumbnail"] = thumbBytes;
        UploadedObjects.Add((bucket, $"{objectName}-thumbnail", thumbBytes));
    }

    public Task<Stream> DownloadFile(Guid fileId, Guid ownerId, CancellationToken ct)
    {
        var key = fileId.ToString();
        if (_store.TryGetValue("alexandria-files", out var bucket) && bucket.TryGetValue(key, out var bytes))
            return Task.FromResult<Stream>(new MemoryStream(bytes));

        throw new InvalidOperationException($"File not found: {fileId}");
    }

    public Task<Stream> DownloadStreamableFile(Guid fileId, Guid userId, CancellationToken ct = default)
        => DownloadFile(fileId, userId, ct);

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

    public async Task StreamFile(string fileId, Stream destination, CancellationToken ct)
    {
        if (_store.TryGetValue("alexandria-files", out var bucket) && bucket.TryGetValue(fileId, out var bytes))
        {
            await destination.WriteAsync(bytes, ct);
            return;
        }

        throw new InvalidOperationException($"File not found: {fileId}");
    }

    public Task<PreviewResultDto?> GetCachedPreview(Guid id, CancellationToken ct)
        => Task.FromResult(PreviewOverride);

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

    public Task<UploadResult> FinalizeFileUpload(string objectName, Guid uploadId, Guid uploadedBy,
        byte[]? encryptionIv, byte[]? encryptionSalt,
        byte[]? integrityTag, string? encryptionHint, int? iterationCount, bool isEncrypted = false,
        Guid? directoryId = null, bool shouldTranspile = false, CancellationToken ct = default)
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

    // public Task<UploadResult> FinalizeFileUpload(
    //     string objectName,
    //     Guid uploadId,
    //     Guid uploadedBy,
    //     Guid? directoryId = null,
    //     CancellationToken ct = default)
    //     => Task.FromResult(new UploadResult(objectName, "fake-checksum", 0, Guid.NewGuid()));

    public Task<StorageBreakdown> GetStorageBreakdown(Guid userId, CancellationToken ct = default)
        => Task.FromResult(new StorageBreakdown
        {
            SizeByType = new Dictionary<string, long>(),
            TrashSize = 0,
            OldFiles = []
        });

    public Task<string> GenerateBackgroundImageGetUrl(string objectKey, TimeSpan expiry)
        => Task.FromResult($"http://fake-s3/bg/{objectKey}");

    public Task DeleteBackgroundImageAsync(string objectKey, CancellationToken ct = default)
    {
        DeletedKeys.Add(objectKey);
        return Task.CompletedTask;
    }

    public Task<string> GenerateImageUploadUrl(string objectKey, TimeSpan expiry)
        => Task.FromResult($"http://fake-s3/upload/{objectKey}");

    public Task<string> GetVersionPresignedUrl(Guid fileVersionId, Guid ownerId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task StreamBulkZipAsync(Guid[] directoryIds, Guid[] fileIds, Guid userId, Stream destination,
        CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetPlaylistCoverUploadUrlAsync(Guid playlistId, Guid userId, string contentType,
        CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetPlaylistCoverUrlAsync(Guid playlistId, Guid userId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}