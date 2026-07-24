using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;

namespace Alexandria.Common.Services;

public interface IPreviewService
{
    /// <summary>
    /// Returns the preview result for the given file, generating or fetching from cache
    /// as appropriate. Returns <c>null</c> when the file is encrypted, or when preview
    /// generation has been dispatched asynchronously and is not yet available.
    /// </summary>
    /// <param name="versionId">The id of the version whose preview we are fetching</param>
    /// <param name="ownerId">The id of the owner of the file whose version we are previewing</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    Task<PreviewResultDto?> GetPreviewUrlAsync(Guid versionId, Guid ownerId, CancellationToken ct = default);

    /// <summary>
    /// Generate a preview for a given file owned by a given user
    /// </summary>
    /// <param name="versionId">File ID</param>
    /// <param name="userId">Owner ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    Task GeneratePreviewAsync(Guid versionId, Guid userId, PreviewKind kind = PreviewKind.Preview,
        CancellationToken ct = default);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="versionId"></param>
    /// <param name="userId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<string?> GetThumbnailAsync(Guid versionId, Guid userId, CancellationToken ct = default);
}