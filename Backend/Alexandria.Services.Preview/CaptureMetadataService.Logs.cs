using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Preview;

public sealed partial class CaptureMetadataService
{
    // Only whether values exist: a location or capture time does not belong in a log file.
    [LoggerMessage(4261, LogLevel.Debug,
        "Stored capture metadata for version {VersionId} (capture time: {HasCaptureTime}, location: {HasLocation})")]
    private static partial void LogStored(ILogger logger, Guid versionId, bool hasCaptureTime, bool hasLocation);
}
