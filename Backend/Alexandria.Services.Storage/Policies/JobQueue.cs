using Alexandria.Common.Policies;
using Alexandria.Common.Services;

namespace Alexandria.Services.Storage.Policies;

public class JobQueue(
    ITranspilationJobService jobService,
    IPreviewService previewService) : IJobQueue
{
    public async Task QueueTranspilationJobAsync(Guid versionId, Guid fileId, Guid userId,
        TranscodeParameters parameters, CancellationToken ct = default)
    {
        //TODO: Add support for dynamic parameters setting.
        var job = await jobService.CreateJobAsync(versionId, userId, parameters.AudioRungs, parameters.VideoRungs, ct);
        await previewService.GeneratePreviewAsync(fileId, userId, ct);
    }

    public Task QueueBackupAsync(Guid fileId, BackupParameters parameters, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task QueueAutoTagAsync(Guid fileId, AutoTagParameters parameters, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}