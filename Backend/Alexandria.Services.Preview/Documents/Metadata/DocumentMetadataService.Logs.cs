using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Preview.Documents.Metadata;

public sealed partial class DocumentMetadataService
{
    // Counts and flags only: names and titles do not belong in a log file.
    [LoggerMessage(4274, LogLevel.Debug,
        "Stored document metadata for version {VersionId} ({Format}, {FieldCount} fields, password protected: {IsPasswordProtected})")]
    private static partial void LogStored(ILogger logger, Guid versionId, string format, int fieldCount,
        bool isPasswordProtected);
}
