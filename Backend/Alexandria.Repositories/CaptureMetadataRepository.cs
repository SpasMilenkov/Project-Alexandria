using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class CaptureMetadataRepository(AlexandriaDbContext context) : ICaptureMetadataRepository
{
    public async Task<CaptureMetadata?> GetByVersionIdAsync(Guid versionId, CancellationToken ct = default)
    {
        return await context.CaptureMetadata
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.FileVersionId == versionId, ct);
    }

    public async Task UpsertAsync(CaptureMetadata metadata, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "CaptureMetadata" (
                "Id", "FileVersionId", "Width", "Height", "CapturedAt", "CapturedOffsetMinutes",
                "Latitude", "Longitude", "CameraMake", "CameraModel", "LensModel", "Software",
                "FocalLengthMm", "FocalLength35mm", "FNumber", "ExposureSeconds", "Iso",
                "RawJson", "ExtractorVersion", "CreatedAt")
            VALUES (
                {metadata.Id}, {metadata.FileVersionId}, {metadata.Width}, {metadata.Height},
                {metadata.CapturedAt}, {metadata.CapturedOffsetMinutes},
                {metadata.Latitude}, {metadata.Longitude},
                {metadata.CameraMake}, {metadata.CameraModel}, {metadata.LensModel}, {metadata.Software},
                {metadata.FocalLengthMm}, {metadata.FocalLength35mm}, {metadata.FNumber},
                {metadata.ExposureSeconds}, {metadata.Iso},
                {metadata.RawJson}::jsonb, {metadata.ExtractorVersion}, {now})
            ON CONFLICT ("FileVersionId") DO UPDATE SET
                "Width" = EXCLUDED."Width",
                "Height" = EXCLUDED."Height",
                "CapturedAt" = EXCLUDED."CapturedAt",
                "CapturedOffsetMinutes" = EXCLUDED."CapturedOffsetMinutes",
                "Latitude" = EXCLUDED."Latitude",
                "Longitude" = EXCLUDED."Longitude",
                "CameraMake" = EXCLUDED."CameraMake",
                "CameraModel" = EXCLUDED."CameraModel",
                "LensModel" = EXCLUDED."LensModel",
                "Software" = EXCLUDED."Software",
                "FocalLengthMm" = EXCLUDED."FocalLengthMm",
                "FocalLength35mm" = EXCLUDED."FocalLength35mm",
                "FNumber" = EXCLUDED."FNumber",
                "ExposureSeconds" = EXCLUDED."ExposureSeconds",
                "Iso" = EXCLUDED."Iso",
                "RawJson" = EXCLUDED."RawJson",
                "ExtractorVersion" = EXCLUDED."ExtractorVersion",
                "UpdatedAt" = {now}
            """, ct);
    }

    public async Task<IReadOnlyList<Guid>> GetExtractionCandidatesAsync(
        int extractorVersion,
        int limit,
        IReadOnlyCollection<Guid> excludedVersionIds,
        CancellationToken ct = default)
    {
        return await context.FileVersions
            .AsNoTracking()
            .Where(v => v.DeletedAt == null
                        && !v.IsEncrypted
                        && v.MimeType.StartsWith("image/")
                        && v.File.DeletedAt == null
                        && v.ContentObject.IsPromoted
                        && !excludedVersionIds.Contains(v.Id)
                        && !context.CaptureMetadata.Any(c =>
                            c.FileVersionId == v.Id && c.ExtractorVersion >= extractorVersion))
            .OrderByDescending(v => v.CreatedAt)
            .Take(limit)
            .Select(v => v.Id)
            .ToListAsync(ct);
    }
}
