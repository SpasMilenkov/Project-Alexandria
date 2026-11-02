using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Workers.MediaMetadata.Config;
using Microsoft.Extensions.Options;

namespace Alexandria.Workers.MediaMetadata.Services;

public partial class StagingService(
    IUnitOfWork unitOfWork,
    IContentStorageService storage,
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
        Directory.CreateDirectory(StagingPaths.IncomingDirectory(stagedDir));

        var stagedPath = StagingPaths.NewIncomingPath(stagedDir, fileId, version.MimeType);

        try
        {
            await storage.DownloadContentObjectAsync(version.ContentObjectId, stagedPath, ct);
        }
        catch
        {
            // The path is unique to this attempt, so the partial download has no other reader.
            Discard(new StagedFile(fileId, stagedPath, version.MimeType));
            throw;
        }

        LogFileStaged(logger, fileId, version.MimeType, stagedPath, new FileInfo(stagedPath).Length);

        return new StagedFile(fileId, stagedPath, version.MimeType);
    }

    public void Discard(StagedFile file)
    {
        if (!StagingPaths.TryDeleteFile(file.FilePath, out var error))
            LogDiscardFailed(logger, error, file.FileId, file.FilePath);
    }
}