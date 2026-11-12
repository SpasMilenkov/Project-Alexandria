using System.Text.Json;
using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Common.Settings.Values;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Api.Features.Admin.Settings.AuditRetention;

internal static class AuditRetentionResponseFactory
{
    public static async Task<AuditRetentionResponse> BuildAsync(
        IAdminSettingsService settings,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        var retention = await settings.GetAuditRetentionAsync(ct);
        var lastPurge = await unitOfWork.AuditLogs.GetLatestAsync(AuditEventCode.AuditLogsPurged, ct);

        return new AuditRetentionResponse
        {
            RetentionDays = retention.RetentionDays,
            MinDays = AuditRetentionValue.MinDays,
            MaxDays = AuditRetentionValue.MaxDays,
            LastPurgeAt = lastPurge?.Timestamp,
            LastPurgeDeletedCount = ReadDeletedCount(lastPurge?.Metadata),
        };
    }

    private static int? ReadDeletedCount(string? metadata)
    {
        if (string.IsNullOrEmpty(metadata))
            return null;

        using var document = JsonDocument.Parse(metadata);

        return document.RootElement.TryGetProperty("deleted_count", out var count) && count.TryGetInt32(out var value)
            ? value
            : null;
    }
}
