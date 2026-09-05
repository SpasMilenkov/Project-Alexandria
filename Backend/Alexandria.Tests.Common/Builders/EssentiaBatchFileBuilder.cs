using Alexandria.Data.Models;

namespace Alexandria.Tests.Common.Builders;

public class EssentiaBatchFileBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _batchId = Guid.NewGuid();
    private Guid _jobId = Guid.NewGuid();
    private Job? _job;
    private Guid _fileId = Guid.NewGuid();
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime? _updatedAt;
    private DateTime? _deletedAt;
    private Guid? _updatedBy;

    public EssentiaBatchFileBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public EssentiaBatchFileBuilder WithBatch(Guid batchId)
    {
        _batchId = batchId;
        return this;
    }

    public EssentiaBatchFileBuilder WithJob(Job job)
    {
        _job = job;
        _jobId = job.Id;
        return this;
    }

    public EssentiaBatchFileBuilder WithJobId(Guid jobId)
    {
        _jobId = jobId;
        return this;
    }

    public EssentiaBatchFileBuilder WithFile(Guid fileId)
    {
        _fileId = fileId;
        return this;
    }

    public EssentiaBatchFileBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public EssentiaBatchFile Build() =>
        new()
        {
            Id = _id,
            BatchId = _batchId,
            JobId = _jobId,
            Job = _job!,
            FileId = _fileId,
            CreatedAt = _createdAt,
            UpdatedAt = _updatedAt,
            DeletedAt = _deletedAt,
            UpdatedBy = _updatedBy
        };
}