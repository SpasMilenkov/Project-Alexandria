using Alexandria.Common.Settings;
using Alexandria.Common.Settings.Values;

namespace Alexandria.Common.Services;

public interface IUserSettingsService
{
    Task<AppearanceSettingsValue> GetAppearanceAsync(Guid userId, CancellationToken ct = default);
    Task<BehaviorSettingsValue> GetBehaviorAsync(Guid userId, CancellationToken ct = default);

    Task SetAppearanceAsync(Guid userId, AppearanceSettingsValue value, Guid updatedBy, CancellationToken ct = default);
    Task SetBehaviorAsync(Guid userId, BehaviorSettingsValue value, Guid updatedBy, CancellationToken ct = default);

    Task<UserSettingsSnapshot> GetAllAsync(Guid userId, CancellationToken ct = default);
    Task ResetAsync(Guid userId, Guid updatedBy, CancellationToken ct = default);
}