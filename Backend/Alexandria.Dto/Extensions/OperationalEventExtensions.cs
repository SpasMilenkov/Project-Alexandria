using System.Text.Json;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Dto.Events.Operational;

namespace Alexandria.Dto.Extensions;

public static class OperationalEventExtensions
{
    // CamelCase + base-typed serialization so the stored blob matches what the
    // frontend expects: camelCase properties plus the "kind" discriminator.
    // Serializing through the abstract base is what makes [JsonPolymorphic]
    // emit the discriminator — serializing the derived type skips it.
    private static readonly JsonSerializerOptions MetadataSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string SerializeMetadata(OperationalEventCode code, OperationalEventMetadata metadata)
    {
        // throws if someone passes a mismatched metadata type for the given code,
        // catching that mistake at the call site instead of at read time
        var expectedType = code switch
        {
            OperationalEventCode.HealthcheckUnhealthy or OperationalEventCode.HealthcheckUnreachable
                => typeof(HealthcheckFailureMetadata),
            OperationalEventCode.ErrorRateThresholdExceeded => typeof(ErrorRateThresholdMetadata),
            OperationalEventCode.UserReportedProblem => typeof(UserReportedMetadata),
            OperationalEventCode.WorkerCycleFailure => typeof(WorkerCycleFailureMetadata),
            _ => throw new ArgumentOutOfRangeException(nameof(code))
        };

        if (metadata.GetType() != expectedType)
            throw new InvalidOperationException($"Metadata type mismatch for code {code}");

        return JsonSerializer.Serialize(metadata, typeof(OperationalEventMetadata), MetadataSerializerOptions);
    }
}