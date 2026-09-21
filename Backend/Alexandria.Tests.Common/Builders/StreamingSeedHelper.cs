using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Tests.Common.Builders;

public sealed record StreamableFileSeed(Guid FileId, Guid JobId, Guid VersionId);

public static class StreamingSeedHelper
{
    public static async Task<StreamableFileSeed> SeedStreamableFileAsync(
        AlexandriaDbContext db,
        Guid ownerId,
        string name,
        DateTime createdAt,
        double durationSeconds,
        bool isVideo = false,
        CancellationToken ct = default)
    {
        var mimeType = isVideo ? "video/mp4" : "audio/mpeg";
        var now = DateTime.UtcNow;

        var co = new ContentObjectBuilder().Build();
        var version = new FileVersionBuilder()
            .WithContentObject(co)
            .WithCreatedBy(ownerId)
            .WithMimeType(mimeType)
            .Build();
        var file = new FileBuilder()
            .WithOwner(ownerId)
            .WithName(name)
            .WithMimeType(mimeType)
            .WithCreatedAt(createdAt)
            .Build();
        file.MediaMetadata = new MediaMetadata
        {
            Id = Guid.NewGuid(),
            FileId = file.Id,
            Duration = durationSeconds,
            CreatedAt = now,
        };

        version.FileId = file.Id;
        version.File = file;

        db.ContentObjects.Add(co);
        db.FileVersions.Add(version);
        db.Files.Add(file);
        await db.SaveChangesAsync(ct);

        file.CurrentVersionId = version.Id;
        db.Files.Update(file);
        await db.SaveChangesAsync(ct);

        var job = new JobBuilder()
            .WithStatus(JobStatus.Ready)
            .WithType(JobType.Transpilation)
            .WithUser(ownerId)
            .WithCompletedAt(now)
            .Build();
        db.Set<Job>().Add(job);
        await db.SaveChangesAsync(ct);

        var transcode = new TranspilationJob
        {
            Id = Guid.NewGuid(),
            VersionId = version.Id,
            JobId = job.Id,
            IsVideo = isVideo,
            UserId = ownerId,
            SegmentPrefix = $"seg-{file.Id:N}",
            CreatedAt = now,
        };
        db.Set<TranspilationJob>().Add(transcode);
        await db.SaveChangesAsync(ct);

        db.Set<StreamingRepresentation>().Add(new StreamingRepresentation
        {
            Id = Guid.NewGuid(),
            TranspilationId = transcode.Id,
            Codec = isVideo ? StreamCodec.H264 : StreamCodec.Opus,
            Status = RepresentationStatus.Ready,
            Size = 1024,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(ct);

        return new StreamableFileSeed(file.Id, transcode.Id, version.Id);
    }

    public static async Task<(Guid PlaylistId, List<Guid> ItemIds)> SeedPlaylistAsync(
        AlexandriaDbContext db,
        Guid ownerId,
        string name,
        IReadOnlyList<Guid> transcodeJobIds,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var playlist = new Playlist
        {
            Id = Guid.NewGuid(),
            Name = name,
            OwnerId = ownerId,
            CreatedAt = now,
        };
        db.Set<Playlist>().Add(playlist);
        await db.SaveChangesAsync(ct);

        var itemIds = new List<Guid>(transcodeJobIds.Count);
        for (var i = 0; i < transcodeJobIds.Count; i++)
        {
            var item = new PlaylistItem
            {
                Id = Guid.NewGuid(),
                PlaylistId = playlist.Id,
                TranspilationJobId = transcodeJobIds[i],
                Position = i,
                CreatedAt = now,
            };
            db.Set<PlaylistItem>().Add(item);
            itemIds.Add(item.Id);
        }

        await db.SaveChangesAsync(ct);
        return (playlist.Id, itemIds);
    }

    public static async Task SeedClosedSessionAsync(
        AlexandriaDbContext db,
        Guid ownerId,
        Guid fileId,
        long listenedSeconds,
        DateTime endedAtUtc,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var history = new StreamHistory
        {
            Id = Guid.NewGuid(),
            UserId = ownerId,
            FileId = fileId,
            LastAccessedAt = now,
            CreatedAt = now,
        };
        db.Set<StreamHistory>().Add(history);
        await db.SaveChangesAsync(ct);

        db.Set<StreamSession>().Add(new StreamSession
        {
            Id = Guid.NewGuid(),
            StreamHistoryId = history.Id,
            ListenedSeconds = listenedSeconds,
            StartPositionSeconds = 0,
            EndPositionSeconds = listenedSeconds,
            StartedAt = endedAtUtc.AddSeconds(-listenedSeconds),
            EndedAt = endedAtUtc,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(ct);
    }
}