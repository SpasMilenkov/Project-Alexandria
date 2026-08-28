using System.Text.Json.Serialization;

namespace Alexandria.Dto.Events.Operational;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(HealthcheckFailureMetadata), "healthcheck-failure")]
[JsonDerivedType(typeof(ErrorRateThresholdMetadata), "error-rate-threshold")]
[JsonDerivedType(typeof(UserReportedMetadata), "user-reported")]
public abstract class OperationalEventMetadata
{
    public required string ServiceInstance { get; init; }
}

public sealed class HealthcheckFailureMetadata : OperationalEventMetadata
{
    public required string CheckName { get; init; }
    public string? Detail { get; init; }
}

public sealed class ErrorRateThresholdMetadata : OperationalEventMetadata
{
    public required int WindowMinutes { get; init; }
    public required int FailureCount { get; init; }
    public required int Threshold { get; init; }
}

public sealed class UserReportedMetadata : OperationalEventMetadata
{
    public required string Description { get; init; }
    public string? PageContext { get; init; }
}