using Alexandria.Data.Models.Enumerators;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Gallery;

public sealed partial class GalleryService
{
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Rejected a gallery cursor from user {UserId} for sort {Sort}; it bypassed endpoint validation")]
    private static partial void LogInvalidCursor(ILogger logger, Guid userId, GallerySort sort);
}