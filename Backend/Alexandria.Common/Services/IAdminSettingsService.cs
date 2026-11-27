using Alexandria.Common.Settings.Values;

namespace Alexandria.Common.Services;

public interface IAdminSettingsService
{
    Task<AuditRetentionValue> GetAuditRetentionAsync(CancellationToken ct = default);

    /// <exception cref="System.ComponentModel.DataAnnotations.ValidationException">Days outside the allowed range.</exception>
    Task SetAuditRetentionAsync(AuditRetentionValue value, Guid updatedBy, CancellationToken ct = default);
}