using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Preview.Documents.BodyText;

internal static partial class BodyTextLog
{
    [LoggerMessage(4290, LogLevel.Warning, "Skipped unreadable document part {PartName}")]
    public static partial void PartSkipped(ILogger logger, Exception ex, string partName);

    [LoggerMessage(4291, LogLevel.Information,
        "Stopped reading document part {PartName} at its {Limit} byte limit; the text is kept up to there")]
    public static partial void PartTooLarge(ILogger logger, string partName, long limit);

    [LoggerMessage(4292, LogLevel.Debug,
        "Read {Characters} characters of {Format} text: {Status}, truncated {IsTruncated}")]
    public static partial void Extracted(ILogger logger, int characters, string format, string status,
        bool isTruncated);
}