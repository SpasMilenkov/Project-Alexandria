using Alexandria.Data.Models;

namespace Alexandria.Common.Repositories;

public interface IUserSettingsRepository : IRepository<UserSettings>
{
    Task<UserSettings?> GetByKeyAsync(string key, Guid userId, CancellationToken ct = default);
    Task<IEnumerable<UserSettings>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}