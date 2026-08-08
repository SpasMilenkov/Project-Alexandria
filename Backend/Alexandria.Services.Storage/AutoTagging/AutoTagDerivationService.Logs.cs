using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.AutoTagging;

public partial class AutoTagDerivationService
{
    [LoggerMessage(
        2101,
        LogLevel.Warning,
        "Auto-tag drift: model key '{Key}' has no taxonomy tag for file {FileId} (analyzer {Analyzer}, version {Version}); no tag created")]
    private static partial void LogDrift(ILogger logger, string key, Guid fileId, string analyzer, string version);
}