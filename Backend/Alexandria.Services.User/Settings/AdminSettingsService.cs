using System.ComponentModel.DataAnnotations;
using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Common.Settings.Keys;
using Alexandria.Common.Settings.Values;
using Alexandria.Data.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Alexandria.Services.User.Settings;

public class AdminSettingsService(
    IUnitOfWork unitOfWork,
    IMemoryCache cache) : IAdminSettingsService
{
    private static readonly MemoryCacheEntryOptions CacheOptions = new()
    {
        Size = 1,
        SlidingExpiration = TimeSpan.FromMinutes(30)
    };

    public Task<UploadPolicyValue> GetUploadPolicyAsync(CancellationToken ct = default)
        => GetAsync<UploadPolicyValue>(AdminSettingKeys.UploadPolicy, ct);

    public async Task SetUploadPolicyAsync(UploadPolicyValue value, Guid updatedBy, CancellationToken ct = default)
    {
        Validate(value);
        await UpsertAsync(AdminSettingKeys.UploadPolicy, value, updatedBy, ct);
    }

    public Task ResetUploadPolicyAsync(Guid updatedBy, CancellationToken ct = default)
        => SetUploadPolicyAsync(new UploadPolicyValue(), updatedBy, ct);

    private async Task<T> GetAsync<T>(string key, CancellationToken ct) where T : new()
    {
        if (cache.TryGetValue(CacheKey(key), out T? cached))
            return cached!;

        var setting = await unitOfWork.AdminSettings.GetByKeyAsync(key, ct);
        var value = TypedSettingAccessor.GetValue<T>(setting?.Value);

        cache.Set(CacheKey(key), value, CacheOptions);
        return value;
    }

    private async Task UpsertAsync<T>(string key, T value, Guid updatedBy, CancellationToken ct)
    {
        var json = TypedSettingAccessor.SetValue(value);
        var existing = await unitOfWork.AdminSettings.GetByKeyAsync(key, ct);

        if (existing is null)
        {
            await unitOfWork.AdminSettings.AddAsync(new AdminSettings
            {
                Key = key,
                Value = json,
                UpdatedBy = updatedBy,
            }, ct);
        }
        else
        {
            existing.Value = json;
            existing.UpdatedBy = updatedBy;
            unitOfWork.AdminSettings.Update(existing);
        }

        await unitOfWork.SaveChangesAsync(ct);

        // Invalidate rather than overwrite-in-place: the next GetAsync repopulates
        // from the just-saved row, so cache and DB can't drift after a concurrent write.
        cache.Remove(CacheKey(key));
    }

    private static void Validate<T>(T value)
    {
        var ctx = new ValidationContext(value!);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(value!, ctx, results, validateAllProperties: true))
            throw new ValidationException(string.Join("; ", results.Select(r => r.ErrorMessage)));
    }

    private static string CacheKey(string settingKey) => $"admin-setting:{settingKey}";
}