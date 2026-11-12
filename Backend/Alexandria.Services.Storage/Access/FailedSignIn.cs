using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Services.Storage.Access;

/// <summary>A failed sign-in row with its metadata already read.</summary>
internal sealed record FailedSignIn(
    Guid? AccountId,
    string? IpAddress,
    LoginFailureReason? Reason,
    string? Email,
    DateTimeOffset Timestamp);
