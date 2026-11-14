using System.Text;
using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Config;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Exceptions.Streaming.Lyrics;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using Alexandria.Dto.Extensions;
using Alexandria.Dto.Files.Streaming.Lyrics;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Streaming;

public class TrackLyricsService(
    IUnitOfWork unitOfWork,
    IPublisherService publisher,
    IMessageBrokerStatus brokerStatus,
    IAuditWriter auditWriter,
    IOptions<ProcessingCapabilities>? capabilities = null) : ITrackLyricsService
{
    private readonly ProcessingCapabilities _capabilities = capabilities?.Value ?? new();

    /// <inheritdoc/>
    public async Task<LyricsDto> GetLyricsForMediaAsync(Guid jobId, Guid userId, CancellationToken ct = default)
    {
        var lyrics = await unitOfWork.Lyrics.FirstOrDefaultAsync(
            l => l.TranspilationJobId == jobId && l.TranspilationJob!.UserId == userId && l.DeletedAt == null, ct);

        if (lyrics is not null) return lyrics.ToDto();

        EnsureLyricsAvailable();

        // A read must not fail or leave a pending row behind while the broker is down; the
        // first read after recovery queues the fetch. Fetching on first view is derived data, like
        // auto-tags and generated previews, so it is not audited the way a user's refetch is.
        if (brokerStatus.IsAvailable)
            await QueueFetchAsync(jobId, userId, recordAudit: false, ct);

        return new LyricsDto
        {
            Status = LyricsStatus.PendingFetch,
            Id = lyrics?.Id ?? Guid.Empty
        };
    }

    /// <inheritdoc/>
    public Task QueueLyricsRefetchAsync(Guid jobId, Guid userId, CancellationToken ct = default)
    {
        return QueueFetchAsync(jobId, userId, recordAudit: true, ct);
    }

    private async Task QueueFetchAsync(Guid jobId, Guid userId, bool recordAudit, CancellationToken ct)
    {
        EnsureLyricsAvailable();
        MessageBrokerUnavailableException.ThrowIfUnavailable(brokerStatus);

        var existing = await unitOfWork.Lyrics.FirstOrDefaultAsync(
            l => l.TranspilationJobId == jobId && l.TranspilationJob!.UserId == userId && l.DeletedAt == null, ct);

        if (existing is not null)
        {
            if (existing.SourceProvider == LyricsProvider.Manual)
                throw new InvalidOperationException(
                    "Cannot queue a refetch for user-submitted lyrics. Remove them first.");

            existing.Status = LyricsStatus.PendingFetch;
            existing.FetchedAt = null;
            unitOfWork.Lyrics.Update(existing);

            if (recordAudit)
            {
                await RecordAsync(AuditEventCode.LyricsRefetchQueued, OperationType.Update, jobId, userId,
                    existing.Id, new Dictionary<string, object?> { ["provider"] = existing.SourceProvider.ToString() },
                    ct);
            }

            await unitOfWork.SaveChangesAsync(ct);

            await RequeueAndPublishAsync(existing.JobId, "lyrics.job", ct);
            return;
        }

        _ = await unitOfWork.TranspilationJobs.FirstOrDefaultAsync(
                t => t.Id == jobId && t.UserId == userId && t.DeletedAt == null, ct)
            ?? throw new LyricsNotFoundException(jobId);

        var job = new Job
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Queued,
            Type = JobType.LyricsFetch,
            UserId = userId
        };
        await unitOfWork.Jobs.AddAsync(job, ct);

        var lyricsId = Guid.NewGuid();
        await unitOfWork.Lyrics.AddAsync(new TrackLyrics
        {
            Id = lyricsId,
            TranspilationJobId = jobId,
            Status = LyricsStatus.PendingFetch,
            JobId = job.Id
        }, ct);

        if (recordAudit)
            await RecordAsync(AuditEventCode.LyricsRefetchQueued, OperationType.Create, jobId, userId, lyricsId,
                null, ct);

        await unitOfWork.SaveChangesAsync(ct);

        await PublishOrFailAsync(job.Id, "lyrics.job", ct);
    }

    // Requeues the linked job in place (same id, clean queued lifecycle) and
    // publishes it. Mirrors the preview dispatch requeue semantics.
    private async Task RequeueAndPublishAsync(Guid jobId, string routingKey, CancellationToken ct)
    {
        await unitOfWork.Jobs.UpdateStatusAsync(jobId, JobStatus.Queued, progress: 0, ct: ct);
        await unitOfWork.Jobs.ClearErrorAsync(jobId, ct);
        await PublishOrFailAsync(jobId, routingKey, ct);
    }

    private async Task PublishOrFailAsync(Guid jobId, string routingKey, CancellationToken ct)
    {
        try
        {
            await publisher.PublishAsync(Encoding.UTF8.GetBytes(jobId.ToString()), routingKey);
        }
        catch (Exception)
        {
            await unitOfWork.Jobs.UpdateStatusAsync(jobId, JobStatus.Failed, errorDetail: "publish failed", ct: ct);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task RemoveLyricsAsync(Guid lyricsId, Guid userId, CancellationToken ct = default)
    {
        var lyrics = await unitOfWork.Lyrics.FirstOrDefaultAsync(
                         l => l.Id == lyricsId && l.TranspilationJob!.UserId == userId, ct)
                     ?? throw new LyricsNotFoundException(lyricsId);

        lyrics.DeletedAt = DateTime.UtcNow;
        unitOfWork.Lyrics.Update(lyrics);
        await RecordAsync(AuditEventCode.LyricsDeleted, OperationType.Delete, lyrics.TranspilationJobId, userId,
            lyrics.Id, new Dictionary<string, object?> { ["provider"] = lyrics.SourceProvider.ToString() }, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task UploadLyricsAsync(
        Guid jobId,
        Guid userId,
        string? plainLyrics,
        string? syncedLyrics,
        CancellationToken ct = default)
    {
        if (plainLyrics is null && syncedLyrics is null)
            throw new ArgumentException("At least one of plain or synced lyrics must be provided.");

        var existing = await unitOfWork.Lyrics.FirstOrDefaultAsync(
            l => l.TranspilationJobId == jobId && l.TranspilationJob!.UserId == userId, ct);

        var lyricsId = existing?.Id ?? Guid.NewGuid();

        if (existing is not null)
        {
            existing.PlainLyrics = plainLyrics;
            existing.SyncedLyrics = syncedLyrics;
            existing.SourceProvider = LyricsProvider.Manual;
            existing.Status = LyricsStatus.Fetched;
            existing.Cached = false;
            existing.ProviderTrackId = null;
            existing.ConfidenceScore = null;
            existing.FetchedAt = DateTime.UtcNow;
            unitOfWork.Lyrics.Update(existing);

            // Manual upload is a completed fetch by definition: the linked job
            // reflects the user-supplied outcome instead of worker output.
            await unitOfWork.Jobs.UpdateStatusAsync(
                existing.JobId, JobStatus.Ready, progress: 100, ct: ct);
            await unitOfWork.Jobs.ClearErrorAsync(existing.JobId, ct);
        }
        else
        {
            _ = await unitOfWork.TranspilationJobs.FirstOrDefaultAsync(
                    t => t.Id == jobId && t.UserId == userId && t.DeletedAt == null, ct)
                ?? throw new LyricsNotFoundException(jobId);

            var job = new Job
            {
                Id = Guid.NewGuid(),
                Status = JobStatus.Ready,
                ProgressPercent = 100,
                CompletedAt = DateTime.UtcNow,
                Type = JobType.LyricsFetch,
                UserId = userId
            };
            await unitOfWork.Jobs.AddAsync(job, ct);

            await unitOfWork.Lyrics.AddAsync(new TrackLyrics
            {
                Id = lyricsId,
                TranspilationJobId = jobId,
                PlainLyrics = plainLyrics,
                SyncedLyrics = syncedLyrics,
                SourceProvider = LyricsProvider.Manual,
                Status = LyricsStatus.Fetched,
                Cached = false,
                FetchedAt = DateTime.UtcNow,
                JobId = job.Id
            }, ct);
        }

        var operation = existing is null ? OperationType.Create : OperationType.Update;

        await RecordAsync(AuditEventCode.LyricsUploaded, operation, jobId, userId, lyricsId,
            new Dictionary<string, object?>
            {
                ["replaced"] = existing is not null,
                ["has_plain"] = plainLyrics is not null,
                ["has_synced"] = syncedLyrics is not null,
            }, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task ChangeProviderAsync(
        Guid lyricsId,
        Guid userId,
        LyricsProvider provider,
        CancellationToken ct = default)
    {
        if (provider == LyricsProvider.Manual)
            throw new ArgumentException("Use UploadLyricsAsync to submit user lyrics.");

        EnsureLyricsAvailable();
        MessageBrokerUnavailableException.ThrowIfUnavailable(brokerStatus);

        var lyrics = await unitOfWork.Lyrics.FirstOrDefaultAsync(
                         l => l.Id == lyricsId && l.TranspilationJob!.UserId == userId, ct)
                     ?? throw new LyricsNotFoundException(lyricsId);

        var previousProvider = lyrics.SourceProvider;
        lyrics.SourceProvider = provider;
        lyrics.Status = LyricsStatus.PendingFetch;
        lyrics.PlainLyrics = null;
        lyrics.SyncedLyrics = null;
        lyrics.ProviderTrackId = null;
        lyrics.ConfidenceScore = null;
        lyrics.FetchedAt = null;
        lyrics.Cached = false;
        unitOfWork.Lyrics.Update(lyrics);
        await RecordAsync(AuditEventCode.LyricsProviderChanged, OperationType.Update, lyrics.TranspilationJobId,
            userId, lyrics.Id, new Dictionary<string, object?>
            {
                ["old"] = previousProvider.ToString(),
                ["new"] = provider.ToString(),
            }, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await RequeueAndPublishAsync(lyrics.JobId, "lyrics.change", ct);
    }

    // Lyrics hang off a transcode, so the row is filed under the track's file to show up in its
    // history. TrackLyrics has no trigger because the lyrics worker rewrites it constantly.
    private async Task RecordAsync(
        AuditEventCode code,
        OperationType operation,
        Guid transpilationJobId,
        Guid userId,
        Guid lyricsId,
        Dictionary<string, object?>? extra,
        CancellationToken ct)
    {
        var track = await unitOfWork.TranspilationJobs.GetTrackFileAsync(transpilationJobId, ct);

        var metadata = new Dictionary<string, object?>
        {
            ["lyrics_id"] = lyricsId,
            ["transpilation_job_id"] = transpilationJobId,
            ["version_id"] = track?.VersionId,
        };

        foreach (var (key, value) in extra ?? [])
            metadata[key] = value;

        auditWriter.Record(new AuditEntry
        {
            EventCode = code,
            OperationType = operation,
            EntityType = EntityType.File,
            EntityId = track?.FileId,
            OwnerId = userId,
            EntityName = track?.FileName,
            Metadata = metadata,
        });
    }

    private void EnsureLyricsAvailable()
    {
        if (!_capabilities.Lyrics)
            throw new CapabilityUnavailableException("Lyrics lookup");
    }
}
