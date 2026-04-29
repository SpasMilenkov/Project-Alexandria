using System.ComponentModel.DataAnnotations;
using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Common.Settings;
using Alexandria.Common.Settings.Keys;
using Alexandria.Common.Settings.Values;
using Alexandria.Data.Models;

namespace Alexandria.Services.User.Settings;

public class UserSettingsService(
    IUnitOfWork unitOfWork) : IUserSettingsService
{
    public Task<AppearanceSettingsValue> GetAppearanceAsync(Guid userId, CancellationToken ct = default)
        => GetAsync<AppearanceSettingsValue>(userId, UserSettingKeys.Appearance, ct);

    public Task<BehaviorSettingsValue> GetBehaviorAsync(Guid userId, CancellationToken ct = default)
        => GetAsync<BehaviorSettingsValue>(userId, UserSettingKeys.Behavior, ct);

    public async Task<UserSettingsSnapshot> GetAllAsync(Guid userId, CancellationToken ct = default)
    {
        var appearanceTask = GetAppearanceAsync(userId, ct);
        var behaviorTask = GetBehaviorAsync(userId, ct);
        await Task.WhenAll(appearanceTask, behaviorTask);

        return new UserSettingsSnapshot(
            await appearanceTask,
            await behaviorTask
        );
    }

    public async Task SetAppearanceAsync(Guid userId, AppearanceSettingsValue value, Guid updatedBy,
        CancellationToken ct = default)
    {
        Validate(value);
        await UpsertAsync(userId, UserSettingKeys.Appearance, value, updatedBy, ct);
    }

    public async Task SetBehaviorAsync(Guid userId, BehaviorSettingsValue value, Guid updatedBy,
        CancellationToken ct = default)
    {
        Validate(value);
        await UpsertAsync(userId, UserSettingKeys.Behavior, value, updatedBy, ct);
    }

    public async Task ResetAsync(Guid userId, Guid updatedBy, CancellationToken ct = default)
    {
        await SetAppearanceAsync(userId, new AppearanceSettingsValue(), updatedBy, ct);
        await SetBehaviorAsync(userId, new BehaviorSettingsValue(), updatedBy, ct);
    }


    private async Task<T> GetAsync<T>(Guid userId, string key, CancellationToken ct) where T : new()
    {
        var setting = await unitOfWork.UserSettings.GetByKeyAsync(key, userId, ct);
        return TypedSettingAccessor.GetValue<T>(setting?.Value);
    }

    private async Task UpsertAsync<T>(Guid userId, string key, T value, Guid updatedBy, CancellationToken ct)
    {
        var json = TypedSettingAccessor.SetValue(value);
        var existing = await unitOfWork.UserSettings.GetByKeyAsync(key, userId, ct);

        if (existing is null)
        {
            await unitOfWork.UserSettings.AddAsync(new UserSettings
            {
                UserId = userId,
                Key = key,
                Value = json,
                UpdatedBy = updatedBy
            }, ct);
        }
        else
        {
            existing.Value = json;
            existing.UpdatedBy = updatedBy;
            unitOfWork.UserSettings.Update(existing);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    private static void Validate<T>(T value)
    {
        var ctx = new ValidationContext(value!);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(value!, ctx, results, validateAllProperties: true))
        {
            var errors = string.Join("; ", results.Select(r => r.ErrorMessage));
            throw new ValidationException($"Invalid setting value: {errors}");
        }
    }
}