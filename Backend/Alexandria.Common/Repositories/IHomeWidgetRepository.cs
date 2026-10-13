using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators.Widgets;

namespace Alexandria.Common.Repositories;

public interface IHomeWidgetRepository : IRepository<HomeWidget>
{
    Task<List<HomeWidget>> GetByUserAsync(Guid userId, HomeLayout? layout = null,
        CancellationToken ct = default);

    Task<HomeWidget?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);

    Task<bool> AnyIdsExistAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}
