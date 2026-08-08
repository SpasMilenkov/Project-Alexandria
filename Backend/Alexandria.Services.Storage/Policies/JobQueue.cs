using Alexandria.Common;
using Alexandria.Common.Policies;
using Alexandria.Common.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Policies;

public class JobQueue(
    ITranspilationJobService jobService,
    IPreviewService previewService,
    IPublisherService publisher,
    IUnitOfWork unitOfWork,
    IConfiguration configuration,
    ILogger<JobQueue> logger) : IJobQueue
{
    private readonly bool _autoTaggingEnabled =
        bool.TryParse(configuration["Features:Autotagging"], out var enabled) && enabled;

    public async Task QueueTranspilationJobAsync(Guid versionId, Guid fileId, Guid userId,
        TranscodeParameters parameters, CancellationToken ct = default)
    {
        //TODO: Add support for dynamic parameters setting.
        var job = await jobService.CreateJobAsync(versionId, userId, parameters.AudioRungs, parameters.VideoRungs, ct);
        await previewService.GeneratePreviewAsync(fileId, userId, ct: ct);
    }

    public Task QueueBackupAsync(Guid fileId, BackupParameters parameters, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task QueueAutoTagAsync(Guid fileId, AutoTagParameters parameters, string mimeType,
        CancellationToken ct = default)
        => AutoTagTrigger.QueueIfNeededAsync(publisher, unitOfWork, _autoTaggingEnabled, fileId, mimeType, logger, ct);
}