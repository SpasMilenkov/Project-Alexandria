using Alexandria.Dto.Files;

namespace Alexandria.Common.Services;

public interface IUploadBatchService
{
    /// <summary>
    /// Finalizes each upload independently. Results line up with <paramref name="items"/>,
    /// and an upload that already finished counts as a success so a lost response can be retried.
    /// </summary>
    Task<IReadOnlyList<UploadFinalizeResult>> FinalizeAsync(
        IReadOnlyList<UploadFinalizeItem> items,
        Guid userId,
        CancellationToken ct = default);
}
