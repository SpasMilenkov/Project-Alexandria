using Alexandria.Common;
using Alexandria.Common.Helpers;
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
        await fileTagService.ApplyAutoTagsAsync(fileId, candidates, behavior.AllowAutoTagRegression, ct);

        LogAutoTagsSynced(logger, fileId, candidates.Count, file.OwnerId);
    }

    /// <summary>
    /// Picks the authoritative enrichment row per facet. For genre, the first backbone in
    /// <see cref="AutoTaggingOptions.ModelPriority"/> that has a non-failure row; for mood,
    /// the single <c>essentia-mood</c> row (mood rows carry no backbone). Failure rows
    /// (<c>{"success":false,...}</c>) never count as authoritative.
    /// </summary>
    private static IReadOnlyList<FileEnrichment> SelectWinners(
        IReadOnlyCollection<FileEnrichment> rows,
        IReadOnlyCollection<string> modelPriority)
    {
        var winners = new List<FileEnrichment>(2);

        foreach (var backbone in modelPriority)
        {
            var analyzer = $"essentia-genre-{backbone.ToLowerInvariant()}";
            var row = rows.FirstOrDefault(r =>
                r.Analyzer.Equals(analyzer, StringComparison.OrdinalIgnoreCase));
            if (row is not null && !EnrichmentPayload.IsFailure(row.PayloadJson))
            {
                winners.Add(row);
                break;
            }
        }

        var mood = rows.FirstOrDefault(r =>
            r.Analyzer.Equals("essentia-mood", StringComparison.OrdinalIgnoreCase));
        if (mood is not null && !EnrichmentPayload.IsFailure(mood.PayloadJson))
            winners.Add(mood);

        return winners;
    }
}