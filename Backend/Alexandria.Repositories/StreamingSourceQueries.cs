using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

internal static class StreamingSourceQueries
{
    internal static IQueryable<TranspilationJob> ViableJobs(
        AlexandriaDbContext context, Guid userId, bool isVideo) =>
        context.TranspilationJobs.Where(j =>
            j.UserId == userId
            && j.DeletedAt == null
            && j.Job.DeletedAt == null
            && j.Job.Status == JobStatus.Ready
            && j.IsVideo == isVideo
            && j.Representations.Any(r =>
                r.DeletedAt == null
                && r.Status == RepresentationStatus.Ready));

    internal static async Task<HashSet<Guid>> GetViableVersionIdsAsync(
        AlexandriaDbContext context, Guid userId, bool isVideo, CancellationToken ct) =>
        (await ViableJobs(context, userId, isVideo)
            .Select(j => j.VersionId)
            .ToListAsync(ct)).ToHashSet();

    internal static IQueryable<PlaylistItem> EligiblePlaylistItems(
        AlexandriaDbContext context, Guid userId, Guid playlistId, bool isVideo) =>
        context.PlaylistItems
            .Where(pi =>
                pi.PlaylistId == playlistId
                && pi.DeletedAt == null
                && pi.Playlist != null
                && pi.Playlist.OwnerId == userId
                && pi.Playlist.DeletedAt == null
                && pi.TranspilationJob != null
                && pi.TranspilationJob.UserId == userId
                && pi.TranspilationJob.DeletedAt == null
                && pi.TranspilationJob.Job.DeletedAt == null
                && pi.TranspilationJob.Job.Status == JobStatus.Ready
                && pi.TranspilationJob.IsVideo == isVideo
                && pi.TranspilationJob.FileVersion.DeletedAt == null
                && pi.TranspilationJob.FileVersion.File.OwnerId == userId
                && pi.TranspilationJob.FileVersion.File.DeletedAt == null
                && pi.TranspilationJob.Representations.Any(r =>
                    r.DeletedAt == null
                    && r.Status == RepresentationStatus.Ready));
}