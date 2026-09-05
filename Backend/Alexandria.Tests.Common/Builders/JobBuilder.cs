using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Tests.Common.Builders;

public class JobBuilder
{
    private Guid _id = Guid.NewGuid();
    private JobStatus _status = JobStatus.Queued;
    private int _progressPercent;
    private int _retryCount;
    private string? _errorDetail;
    private DateTime? _startedAt;
    private DateTime? _completedAt;
    private JobType _type = JobType.Transpilation;
    private Guid _userId = Guid.NewGuid();
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime? _updatedAt;
    private DateTime? _deletedAt;
    private Guid? _updatedBy;

    public JobBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public JobBuilder WithStatus(JobStatus status)
    {
        _status = status;
        return this;
    }

    public JobBuilder WithType(JobType type)
    {
        _type = type;
        return this;
    }

    public JobBuilder WithUser(Guid userId)
    {
        _userId = userId;
        return this;
    }

    public JobBuilder WithProgress(int progressPercent)
    {
        _progressPercent = progressPercent;
        return this;
    }

    public JobBuilder WithRetryCount(int retryCount)
    {
        _retryCount = retryCount;
        return this;
    }

    public JobBuilder WithError(string? errorDetail)
    {
        _errorDetail = errorDetail;
        return this;
    }

    public JobBuilder WithStartedAt(DateTime? startedAt)
    {
        _startedAt = startedAt;
        return this;
    }

    public JobBuilder WithCompletedAt(DateTime? completedAt)
    {
        _completedAt = completedAt;
        return this;
    }

    public JobBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public JobBuilder WithDeletedAt(DateTime? deletedAt)
    {
        _deletedAt = deletedAt;
        return this;
    }

    public Job Build() =>
        new()
        {
            Id = _id,
            Status = _status,
            ProgressPercent = _progressPercent,
            RetryCount = _retryCount,
            ErrorDetail = _errorDetail,
            StartedAt = _startedAt,
            CompletedAt = _completedAt,
            Type = _type,
            UserId = _userId,
            CreatedAt = _createdAt,
            UpdatedAt = _updatedAt,
            DeletedAt = _deletedAt,
            UpdatedBy = _updatedBy
        };
}