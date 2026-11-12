namespace Alexandria.Api.Features.Admin.Settings.AuditRetention;

public sealed class AuditRetentionResponse
{
    public required int RetentionDays { get; init; }
    public required int MinDays { get; init; }
    public required int MaxDays { get; init; }
    public DateTimeOffset? LastPurgeAt { get; init; }
    public int? LastPurgeDeletedCount { get; init; }
}
