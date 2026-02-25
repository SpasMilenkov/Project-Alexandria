using Models;

namespace Common.Repositories;

public interface IAdminSettingsRepository : IRepository<AdminSettings>
{
    Task<AdminSettings?> GetByKeyAsync(string key, CancellationToken ct = default);
}
