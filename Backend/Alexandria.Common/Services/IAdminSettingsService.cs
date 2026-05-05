using Alexandria.Common.Settings.Values;

namespace Alexandria.Common.Services;

public interface IAdminSettingsService
{
    Task<UploadPolicyValue> GetUploadPolicyAsync(CancellationToken ct = default);
    Task SetUploadPolicyAsync(UploadPolicyValue value, Guid updatedBy, CancellationToken ct = default);
    Task ResetUploadPolicyAsync(Guid updatedBy, CancellationToken ct = default);
}