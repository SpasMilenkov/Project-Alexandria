namespace Alexandria.Api.Features.Admin.Settings.AuditRetention;

public sealed class UpdateAuditRetentionRequest
{
    public int RetentionDays { get; set; }
}
