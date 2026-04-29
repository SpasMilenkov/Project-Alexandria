using System.ComponentModel.DataAnnotations;
using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Common.Settings.Keys;
using Alexandria.Common.Settings.Values;
using Alexandria.Data.Models;

namespace Alexandria.Services.User.Settings;

public class AdminSettingsService(
    IUnitOfWork unitOfWork) : IAdminSettingsService
{
    public Task<UploadPolicyValue> GetUploadPolicyAsync(CancellationToken ct = default)
        => GetAsync<UploadPolicyValue>(AdminSettingKeys.UploadPolicy, ct);

    public async Task SetUploadPolicyAsync(UploadPolicyValue value, Guid updatedBy, CancellationToken ct = default)
    {
        Validate(value);
        await UpsertAsync(AdminSettingKeys.UploadPolicy, value, updatedBy, ct);
    }

    public Task ResetUploadPolicyAsync(Guid updatedBy, CancellationToken ct = default)
        => SetUploadPolicyAsync(new UploadPolicyValue(), updatedBy, ct);

    // ── Private helpers ─────────────────────────────────────────────────────

    private async Task<T> GetAsync<T>(string key, CancellationToken ct) where T : new()
    {
        var setting = await unitOfWork.AdminSettings.GetByKeyAsync(key, ct);
        return TypedSettingAccessor.GetValue<T>(setting?.Value);
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
    }

    private static void Validate<T>(T value)
    {
        var ctx = new ValidationContext(value!);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(value!, ctx, results, validateAllProperties: true))
            throw new ValidationException(string.Join("; ", results.Select(r => r.ErrorMessage)));
    }
}