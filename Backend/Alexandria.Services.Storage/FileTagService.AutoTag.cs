using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Autotag;

namespace Alexandria.Services.Storage;

public partial class FileTagService
{
    public async Task ApplyAutoTagsAsync(
        Guid fileId,
        IReadOnlyCollection<TagCandidate> candidates,
        bool allowAutoTagRegression,
        CancellationToken ct = default)
    {
        var file = await unitOfWork.Files.GetFileEntityWithTagsAsync(fileId, ct);
        if (file is null)
            throw new InvalidOperationException($"File {fileId} not found");

        var candidateIds = candidates.Select(c => c.TagId).ToHashSet();
        var now = DateTime.UtcNow;
        var applied = 0;
        var pruned = 0;

        try
        {
            await unitOfWork.BeginTransactionAsync(ct);

            foreach (var candidate in candidates)
            {
                var existing = file.FileTags!.FirstOrDefault(ft => ft.TagId == candidate.TagId);

                if (existing is null)
                {
                    file.FileTags!.Add(new FileTag
                    {
                        FileId = fileId,
                        TagId = candidate.TagId,
                        Source = TagSource.Auto,
                        Confidence = candidate.Confidence,
                        CreatedAt = now,
                        UpdatedAt = now,
                    });
                    applied++;
                    continue;
                }

                switch (existing.Source)
                {
                    case TagSource.User:
                    case TagSource.Suppressed:
                        // User decision wins; an Auto row would also be re-created later.
                        continue;

                    case TagSource.Auto:
                        if (allowAutoTagRegression
                            || existing.Confidence is null
                            || candidate.Confidence >= existing.Confidence)
                        {
                            existing.Confidence = candidate.Confidence;
                            existing.UpdatedAt = now;
                            applied++;
                        }

                        continue;
                    default:
                        // FileName/FileMetadata matched a taxonomy candidate → promote.
                        existing.Source = TagSource.Auto;
                        existing.Confidence = candidate.Confidence;
                        existing.UpdatedAt = now;
                        applied++;
                        continue;
                }
            }

            // Prune-to-candidate-set: only facets that produced candidates this run.
            // Stale Auto rows are hard-removed (not tombstoned — the model may re-verdict).
            var candidateFacets = candidates.Select(c => c.Facet).Distinct().ToList();
            foreach (var facet in candidateFacets)
            {
                var stale = file.FileTags!
                    .Where(ft => ft.Source == TagSource.Auto
                                 && ft.Tag?.Facet == facet
                                 && !candidateIds.Contains(ft.TagId))
                    .ToList();

                foreach (var row in stale)
                {
                    file.FileTags!.Remove(row);
                    pruned++;
                }
            }

            await unitOfWork.SaveChangesAsync(ct);
            await unitOfWork.CommitAsync(ct);

            LogAutoTagsApplied(logger, fileId, applied, pruned);
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackAsync(ct);
            LogAutoTagsApplyFailed(logger, ex, fileId);
            throw;
        }
    }
}