using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Dto.StorageStats;

namespace Alexandria.Services.Monitoring;

public class AdminStorageStatsService(
    IFileRepository fileRepository,
    IPreviewRepository previewRepository,
    IStreamingRepresentationRepository representationRepository,
    IUserRepository userRepository) : IAdminStorageStatsService
{
    public async Task<StorageSplitResponse> GetSplitAsync(CancellationToken ct)
    {
        var files = await fileRepository.GetLiveSizeByOwnerAsync(ct);
        var previews = await previewRepository.GetSizeByOwnerAsync(ct);
        var transcoded = await representationRepository.GetSizeByOwnerAsync(ct);
        var trash = await fileRepository.GetTotalTrashSizeAsync(ct);

        return new StorageSplitResponse(
            files.Values.Sum(),
            previews.Values.Sum(),
            transcoded.Values.Sum(),
            trash);
    }

    public async Task<IReadOnlyList<UserStorageRankDto>> GetUserRankingAsync(int top, CancellationToken ct)
    {
        var files = await fileRepository.GetLiveSizeByOwnerAsync(ct);
        var previews = await previewRepository.GetSizeByOwnerAsync(ct);
        var transcoded = await representationRepository.GetSizeByOwnerAsync(ct);
        var users = await userRepository.FindAsync(u => u.DeletedAt == null, ct);

        return users
            .Select(u => BuildRank(u.Id, u.UserName ?? "unknown", u.StorageQuota, files, previews,
                transcoded))
            .Where(r => r.UsedBytes > 0)
            .OrderByDescending(r => r.UsedBytes)
            .Take(top)
            .ToList();
    }

    private static UserStorageRankDto BuildRank(
        Guid userId, string userName, long quota,
        Dictionary<Guid, long> files, Dictionary<Guid, long> previews, Dictionary<Guid, long> transcoded)
    {
        var filesSize = files.GetValueOrDefault(userId);
        var previewsSize = previews.GetValueOrDefault(userId);
        var transcodedSize = transcoded.GetValueOrDefault(userId);

        return new UserStorageRankDto(
            userId,
            userName,
            filesSize,
            previewsSize,
            transcodedSize,
            filesSize + previewsSize + transcodedSize,
            quota);
    }
}