using Alexandria.Common;
using Alexandria.Common.Helpers;
using Alexandria.Common.Playlists;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Storage.AutoTagging;

/// <summary>
/// Per-file auto-tag pipeline: loads the file, picks the authoritative enrichment row per
/// facet (highest-priority backbone in <c>ModelPriority</c> with a successful payload),
/// derives candidates via <see cref="IAutoTagDerivationService"/>, then applies them via
/// <see cref="IFileTagService"/> honouring the owner's <c>AllowAutoTagRegression</c>.
/// Runs on its own scoped <see cref="IUnitOfWork"/> per file (drained by
/// <c>AutoTagSyncWorker</c>), so it never sees another file's uncommitted rows.
/// </summary>
public partial class AutoTagSyncService(
    IUnitOfWork unitOfWork,
    IOptions<AutoTaggingOptions> options,
    IUserSettingsService userSettingsService,
    IAutoTagDerivationService derivationService,
    IFileTagService fileTagService,
    IOutbox outbox,
    ILogger<AutoTagSyncService> logger) : IAutoTagSyncService
{
    public async Task SyncFileAsync(Guid fileId, CancellationToken ct = default)
    {
        var file = await unitOfWork.Files.GetByIdAsync(fileId, ct);
        if (file is null || file.DeletedAt is not null)
        {
            LogFileSkipped(logger, fileId, "file not found or deleted");
            return;
        }

        var rows = (await unitOfWork.FileEnrichments
                .FindAsync(e => e.FileId == fileId, ct))
            .ToList();
        if (rows.Count == 0)
        {
            LogFileSkipped(logger, fileId, "no enrichment rows");
            return;
        }

        var authoritative = SelectWinners(rows, options.Value.ModelPriority);
        if (authoritative.Count == 0)
        {
            LogFileSkipped(logger, fileId, "no authoritative enrichment rows");
            return;
        }

        var candidates = await derivationService.DeriveAsync(authoritative, ct);
        if (candidates.Count == 0)
        {
            LogFileSkipped(logger, fileId, "no candidates derived");
            return;
        }

        var behavior = await userSettingsService.GetBehaviorAsync(file.OwnerId, ct);

        // The tag write opens its own transaction, which nests inside this one, so the tags and
        // the playlist notice commit together.
        await unitOfWork.InTransactionAsync(async () =>
        {
            await fileTagService.ApplyAutoTagsAsync(fileId, candidates, behavior.AllowAutoTagRegression, ct);
            await outbox.EnqueueAsync(
                PlaylistSyncMessage.SyncRoutingKey, PlaylistSyncMessage.Encode(fileId, file.OwnerId), ct);
        }, ct);

        LogAutoTagsSynced(logger, fileId, candidates.Count, file.OwnerId);
    }

    /// <summary>
    /// Picks the authoritative enrichment row per facet. For genre, the first backbone in
    /// <see cref="AutoTaggingOptions.ModelPriority"/> that has a successful row; for mood, the
    /// <c>essentia-mood</c> row (mood rows carry no backbone). Failure rows
    /// (<c>{"success":false,...}</c>) never count as authoritative.
    /// </summary>
    private static IReadOnlyList<FileEnrichment> SelectWinners(
        IReadOnlyCollection<FileEnrichment> rows,
        IReadOnlyCollection<string> modelPriority)
    {
        var winners = new List<FileEnrichment>(2);

        foreach (var backbone in modelPriority)
        {
            var row = NewestSuccess(rows, $"essentia-genre-{backbone.ToLowerInvariant()}");
            if (row is not null)
            {
                winners.Add(row);
                break;
            }
        }

        var mood = NewestSuccess(rows, "essentia-mood");
        if (mood is not null)
            winners.Add(mood);

        return winners;
    }

    // Rows are kept per model version, so one analyzer can hold a timeout failure filed under
    // "unknown" next to a real result. Only successful rows compete, the newest winning.
    private static FileEnrichment? NewestSuccess(IReadOnlyCollection<FileEnrichment> rows, string analyzer)
        => rows
            .Where(r => r.Analyzer.Equals(analyzer, StringComparison.OrdinalIgnoreCase)
                        && !EnrichmentPayload.IsFailure(r.PayloadJson))
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .FirstOrDefault();
}