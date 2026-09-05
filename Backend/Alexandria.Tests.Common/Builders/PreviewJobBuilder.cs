using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Tests.Common.Builders;

public class PreviewJobBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _jobId = Guid.NewGuid();
    private Job? _job;
    private Guid _versionId = Guid.NewGuid();
    private PreviewKind _kind = PreviewKind.Preview;
    private Guid _userId = Guid.NewGuid();
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime? _updatedAt;
    private DateTime? _deletedAt;
    private Guid? _updatedBy;

    public PreviewJobBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public PreviewJobBuilder WithJob(Job job)
    {
        _job = job;
        _jobId = job.Id;
        return this;
    }

    public PreviewJobBuilder WithJobId(Guid jobId)
    {
        _jobId = jobId;
        return this;
    }

    public PreviewJobBuilder WithVersion(Guid versionId)
    {
        _versionId = versionId;
        return this;
    }

    public PreviewJobBuilder WithKind(PreviewKind kind)
    {
        _kind = kind;
        return this;
    }

    public PreviewJobBuilder WithUser(Guid userId)
    {
        _userId = userId;
        return this;
    }

    public PreviewJobBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public PreviewJob Build() =>
        new()
        {
            Id = _id,
            JobId = _jobId,
            Job = _job!,
            VersionId = _versionId,
            Kind = _kind,
            UserId = _userId,
            CreatedAt = _createdAt,
            UpdatedAt = _updatedAt,
            DeletedAt = _deletedAt,
            UpdatedBy = _updatedBy
        };
}