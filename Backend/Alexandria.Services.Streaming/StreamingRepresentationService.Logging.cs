using Alexandria.Data.Models.Enumerators;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming;

public partial class StreamingRepresentationService
{
    [LoggerMessage(6000, LogLevel.Information,
        "Streaming representation {RepresentationId} created for job {JobId} with codec {Codec}")]
    private static partial void LogRepresentationCreated(
        ILogger logger, Guid representationId, Guid jobId, StreamCodec codec);

    [LoggerMessage(6001, LogLevel.Information,
        "Streaming representation {RepresentationId} marked ready at segment prefix '{SegmentPrefix}'")]
    private static partial void LogRepresentationMarkedReady(
        ILogger logger, Guid representationId, string segmentPrefix);

    [LoggerMessage(6002, LogLevel.Warning,
        "Streaming representation {RepresentationId} marked failed")]
    private static partial void LogRepresentationMarkedFailed(
        ILogger logger, Guid representationId);

    [LoggerMessage(6003, LogLevel.Debug,
        "Streaming representation {RepresentationId} marked processing")]
    private static partial void LogRepresentationMarkedProcessing(
        ILogger logger, Guid representationId);
}