using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Previews;

namespace Alexandria.Tests.Common.Fakes;

public class FakePreviewArtifactService : IPreviewArtifactService
{
    public Task UploadPreview(string objectName, string contentType, Stream fileStream, Guid versionId,
        Guid uploadedBy, long contentLength, PreviewKind kind, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task UploadMediaData(Stream previewStream, Stream thumbnailStream, long previewSize, long thumbnailSize,
        string objectName, Guid versionId, MediaMetadataDto metadataDto, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<string?> GetCachedPreview(Guid versionId, PreviewKind kind, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<PaginatedResult<UserPreviewDto>> GetMyPreviewsAsync(
        Guid userId, Guid? fileId, DateTime? createdBefore, int page, int pageSize,
        CancellationToken ct = default)
        => Task.FromResult(new PaginatedResult<UserPreviewDto>
        {
            Items = [],
            TotalCount = 0,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = 0
        });

    public Task<long> DeletePreviewAsync(
        Guid previewId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<(int DeletedCount, long FreedBytes)> DeletePreviewsByFileAsync(
        Guid fileId, Guid userId, bool isAdmin, DateTime? createdBefore, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}