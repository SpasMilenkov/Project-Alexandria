namespace Alexandria.Dto.Audit;

/// <summary>The few columns the access overview needs from a sign-in or failed sign-in row.</summary>
public sealed record AuditSignInRow(Guid? AccountId, string? IpAddress, string? MetadataJson, DateTimeOffset Timestamp);
