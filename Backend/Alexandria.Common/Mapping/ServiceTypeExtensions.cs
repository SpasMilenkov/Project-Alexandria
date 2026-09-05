using Alexandria.Data.Models.Enumerators;
using Alexandria.Data.Models.Enumerators.Monitoring;

namespace Alexandria.Common.Mapping;

public static class ServiceTypeExtensions
{
    /// <summary>
    /// Maps a service to the JobType backing its outcome data, or null if the
    /// service has no Job table and is monitored some other way (e.g. Api,
    /// which goes through the healthcheck publisher).
    /// </summary>
    public static JobType? ToJobType(this ServiceType serviceType) => serviceType switch
    {
        ServiceType.MediaPreviews => JobType.MediaPreview,
        ServiceType.DocumentPreviews => JobType.DocumentPreview,
        ServiceType.Transpilation => JobType.Transpilation,
        ServiceType.Lyrics => JobType.LyricsFetch,
        ServiceType.MediaMetadata => JobType.MetadataEnrichment,
        ServiceType.Api => null,
        _ => throw new ArgumentOutOfRangeException(nameof(serviceType), serviceType, null)
    };
}