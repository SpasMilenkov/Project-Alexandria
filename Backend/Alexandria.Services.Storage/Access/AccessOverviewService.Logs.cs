using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Access;

public sealed partial class AccessOverviewService
{
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Failed sign-in row from {Timestamp} has unreadable metadata; counted without reason or email")]
    private static partial void LogUnreadableFailure(ILogger logger, DateTimeOffset timestamp, Exception ex);
}
