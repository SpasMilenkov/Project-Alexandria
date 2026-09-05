using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Tests.Common.Builders;

public class TrackLyricsBuilder
{
    private Guid _id = Guid.NewGuid();
    private LyricsStatus _status = LyricsStatus.PendingFetch;
    private Guid _transpilationJobId = Guid.NewGuid();
    private Guid _jobId = Guid.NewGuid();
    private Job? _job;
    private LyricsProvider _provider = LyricsProvider.LrclibPublic;
    private DateTime? _fetchedAt;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime? _updatedAt;
    private DateTime? _deletedAt;
    private Guid? _updatedBy;

    public TrackLyricsBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public TrackLyricsBuilder WithStatus(LyricsStatus status)
    {
        _status = status;
        return this;
    }

    public TrackLyricsBuilder WithTranspilationJob(Guid transpilationJobId)
    {
        _transpilationJobId = transpilationJobId;
        return this;
    }

    public TrackLyricsBuilder WithJob(Job job)
    {
        _job = job;
        _jobId = job.Id;
        return this;
    }

    public TrackLyricsBuilder WithJobId(Guid jobId)
    {
        _jobId = jobId;
        return this;
    }

    public TrackLyricsBuilder WithFetchedAt(DateTime? fetchedAt)
    {
        _fetchedAt = fetchedAt;
        return this;
    }

    public TrackLyricsBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public TrackLyrics Build() =>
        new()
        {
            Id = _id,
            Status = _status,
            TranspilationJobId = _transpilationJobId,
            JobId = _jobId,
            Job = _job!,
            SourceProvider = _provider,
            FetchedAt = _fetchedAt,
            CreatedAt = _createdAt,
            UpdatedAt = _updatedAt,
            DeletedAt = _deletedAt,
            UpdatedBy = _updatedBy
        };
}