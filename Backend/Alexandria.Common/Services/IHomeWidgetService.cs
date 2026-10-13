using Alexandria.Dto.Home;

namespace Alexandria.Common.Services;

public interface IHomeWidgetService
{
    Task<HomeBoardDto> GetAsync(Guid userId, CancellationToken ct = default);

    Task<HomeBoardDto> SaveAsync(Guid userId, HomeBoardChanges changes, CancellationToken ct = default);

    Task<HomeBoardDto> ResetAsync(Guid userId, CancellationToken ct = default);
}
