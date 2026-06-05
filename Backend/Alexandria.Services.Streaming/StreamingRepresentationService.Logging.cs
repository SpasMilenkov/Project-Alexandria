using Alexandria.Data.Models.Enumerators;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming;

public partial class StreamingRepresentationService
{
    [LoggerMessage(2000, LogLevel.Information,
        "Representation {RepresentationId} created for job {JobId} with codec {Codec}")]
    private static partial void LogRepresentationCreated(
        ILogger logger, Guid representationId, Guid jobId, StreamCodec codec);

    [LoggerMessage(2001, LogLevel.Information,
        "{Count} representations created for job {JobId}")]
    private static partial void LogRepresentationsCreated(
        ILogger logger, int count, Guid jobId);

    [LoggerMessage(2002, LogLevel.Information,
        "Representation {RepresentationId} marked Ready")]
    private static partial void LogRepresentationMarkedReady(
        ILogger logger, Guid representationId);

    [LoggerMessage(2003, LogLevel.Information,
        "Representation {RepresentationId} marked Failed")]
    private static partial void LogRepresentationMarkedFailed(
        ILogger logger, Guid representationId);

    [LoggerMessage(2004, LogLevel.Information,
        "Representation {RepresentationId} marked Processing")]
    private static partial void LogRepresentationMarkedProcessing(
        ILogger logger, Guid representationId);

    [LoggerMessage(2005, LogLevel.Information,
        "{Count} representations marked Ready")]
    private static partial void LogRepresentationsMarkedReady(
        ILogger logger, int count);

    [LoggerMessage(2006, LogLevel.Warning,
        "{Count} representations marked Failed")]
    private static partial void LogRepresentationsMarkedFailed(
        ILogger logger, int count);
}