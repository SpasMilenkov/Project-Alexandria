using Microsoft.Extensions.Logging;

namespace Alexandria.Services.User;

public partial class UserAvatarService
{
    [LoggerMessage(LogLevel.Debug, "Removed {Count} old profile picture(s) for user {UserId}")]
    private static partial void LogAvatarsSwept(ILogger logger, int count, Guid userId);

    [LoggerMessage(LogLevel.Warning, "Could not remove old profile pictures for user {UserId}")]
    private static partial void LogAvatarSweepFailed(ILogger logger, Exception ex, Guid userId);
}
