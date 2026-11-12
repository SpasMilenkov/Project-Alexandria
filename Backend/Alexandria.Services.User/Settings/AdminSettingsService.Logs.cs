using Microsoft.Extensions.Logging;

namespace Alexandria.Services.User.Settings;

public partial class AdminSettingsService
{
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Stored audit retention of {StoredDays} days is out of range; using {EffectiveDays} days instead")]
    private static partial void LogRetentionOutOfRange(ILogger logger, int storedDays, int effectiveDays);
}
