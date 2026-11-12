using Alexandria.Common.Settings.Values;

namespace Alexandria.Common.Services;

public interface IAdminSettingsService
{
    Task<UploadPolicyValue> GetUploadPolicyAsync(CancellationToken ct = default);
    Task SetUploadPolicyAsync(UploadPolicyValue value, Guid updatedBy, CancellationToken ct = default);
    Task ResetUploadPolicyAsync(Guid updatedBy, CancellationToken ct = default);

    Task<AuditRetentionValue> GetAuditRetentionAsync(CancellationToken ct = default);

    /// <exception cref="System.ComponentModel.DataAnnotations.ValidationException">Days outside the allowed range.</exception>
    Task SetAuditRetentionAsync(AuditRetentionValue value, Guid updatedBy, CancellationToken ct = default);
}