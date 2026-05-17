using Alexandria.Dto.Files;

namespace Alexandria.Common.Services;

public interface IPreviewService
{
    /// <summary>
    /// Returns the preview result for the given file, generating or fetching from cache
    /// as appropriate. Returns <c>null</c> when the file is encrypted, or when preview
    /// generation has been dispatched asynchronously and is not yet available.
    /// </summary>
    Task<PreviewResultDto?> GetPreviewUrlAsync(Guid fileId, Guid ownerId, CancellationToken ct = default);

    /// <summary>
    /// Returns <c>true</c> when a completed preview artifact already exists in storage
    /// for the given file version. Used by the transpilation worker to skip redundant
    /// preview generation when the preview service has already run.
    /// </summary>
    Task<bool> HasPreviewAsync(Guid versionId, CancellationToken ct = default);
}