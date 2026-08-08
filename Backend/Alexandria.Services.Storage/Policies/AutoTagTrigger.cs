using System.Text;
using Alexandria.Common;
using Alexandria.Common.Exceptions.Policies;
using Alexandria.Common.Helpers;
using Alexandria.Common.Services;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Policies;

/// <summary>
/// Shared auto-tag trigger path used by both policy dispatch (<see cref="JobQueue"/>) and the
/// dedicated <c>/files/{fileId}/auto-tag</c> endpoint: refuses with
/// <see cref="AutoTaggingDisabledException"/> when the feature flag is off, silently skips
/// files whose MIME type is not routable to the audio pipeline, skips files that already have
/// a successful enrichment (idempotent, since failure rows share the analyzer keys of later
/// retries), and otherwise publishes a <c>media-metadata.enrich</c> trigger.
/// </summary>
public static partial class AutoTagTrigger
{
    public const string EnrichRoutingKey = "media-metadata.enrich";

    /// <summary>
    /// Returns true when an enrich trigger was published, false when the file was skipped
    /// (unsupported MIME type or already successfully enriched). Throws
    /// <see cref="AutoTaggingDisabledException"/> when the feature flag is off.
    /// </summary>
    public static async Task<bool> QueueIfNeededAsync(
        IPublisherService publisher,
        IUnitOfWork unitOfWork,
        bool autoTaggingEnabled,
        Guid fileId,
        string mimeType,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        if (unitOfWork == null) throw new ArgumentNullException(nameof(unitOfWork));
        if (!autoTaggingEnabled)
            throw new AutoTaggingDisabledException();

        if (!AutoTagSupportedFileTypes.IsSupported(mimeType))
        {
            if (logger is not null)
                LogAutoTagSkippedUnsupportedType(logger, fileId, mimeType);
            return false;
        }

        if (await unitOfWork.FileEnrichments.HasSuccessfulAutoTagEnrichmentAsync(fileId, ct))
            return false;

        await publisher.PublishAsync(
            Encoding.UTF8.GetBytes(fileId.ToString()),
            EnrichRoutingKey);

        return true;
    }

    [LoggerMessage(3001, LogLevel.Information,
        "Auto-tag skipped for file {FileId}: MIME type '{MimeType}' is not supported")]
    private static partial void LogAutoTagSkippedUnsupportedType(ILogger logger, Guid fileId, string mimeType);
}