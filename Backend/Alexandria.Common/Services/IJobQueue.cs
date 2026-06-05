using Alexandria.Common.Policies;

namespace Alexandria.Common.Services;

public interface IJobQueue
{
    Task QueueTranspilationJobAsync(Guid versionId, Guid fileId, Guid userId, TranscodeParameters parameters,
        CancellationToken ct = default);

    Task QueueBackupAsync(Guid fileId, BackupParameters parameters, CancellationToken ct = default);
    Task QueueAutoTagAsync(Guid fileId, AutoTagParameters parameters, CancellationToken ct = default);
}