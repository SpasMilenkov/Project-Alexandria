using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Workers.MediaMetadata.Config;
using Microsoft.Extensions.Options;

namespace Alexandria.Workers.MediaMetadata.Services;

public partial class StagingService(
    IUnitOfWork unitOfWork,
    IStorageService storage,
    IOptions<EssentiaConfig> options,
    ILogger<StagingService> logger) : IStagingService
{
    public async Task<StagedFile?> StageAsync(Guid fileId, CancellationToken ct = default)
    {
        var file = await unitOfWork.Files.GetByIdAsync(fileId, ct);
        if (file is null)
        {
            LogFileNotFound(logger, fileId);
            return null;
        }

        //TODO: might unify this in a single db call if I see it is repeated later
        var version = await unitOfWork.FileVersions.FirstOrDefaultAsync(
            v => v.Id == file.CurrentVersionId && v.DeletedAt == null, ct);

        if (version is null)
        {
            LogVersionNotFound(logger, fileId);
            return null;
        }

        var stagedDir = options.Value.StagedDir;
        Directory.CreateDirectory(stagedDir);

        var extension = MimeTypeExtensions.GetExtension(version.MimeType);
        var stagedPath = Path.Combine(stagedDir, $"{fileId}{extension}");

        await storage.DownloadContentObjectAsync(version.ContentObjectId, stagedPath, ct);

        LogFileStaged(logger, fileId, version.MimeType, stagedPath, new FileInfo(stagedPath).Length);

        return new StagedFile(fileId, stagedPath, version.MimeType);
    }
}