using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Tests.Common.Builders;

public class EssentiaBatchBuilder
{
    private Guid _id = Guid.NewGuid();
    private EssentiaBackbone _backbone = EssentiaBackbone.Effnet;
    private EssentiaBatchStatus _status = EssentiaBatchStatus.Dispatched;
    private DateTime? _dispatchedAt = DateTime.UtcNow;
    private DateTime? _completedAt;
    private ICollection<EssentiaBatchFile> _files = [];
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime? _updatedAt;
    private DateTime? _deletedAt;
    private Guid? _updatedBy;

    public EssentiaBatchBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public EssentiaBatchBuilder WithBackbone(EssentiaBackbone backbone)
    {
        _backbone = backbone;
        return this;
    }

    public EssentiaBatchBuilder WithStatus(EssentiaBatchStatus status)
    {
        _status = status;
        return this;
    }

    public EssentiaBatchBuilder WithDispatchedAt(DateTime? dispatchedAt)
    {
        _dispatchedAt = dispatchedAt;
        return this;
    }

    public EssentiaBatchBuilder WithFiles(ICollection<EssentiaBatchFile> files)
    {
        _files = files;
        return this;
    }

    public EssentiaBatchBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public EssentiaBatch Build() =>
        new()
        {
            Id = _id,
            Backbone = _backbone,
            Status = _status,
            DispatchedAt = _dispatchedAt,
            CompletedAt = _completedAt,
            Files = _files,
            CreatedAt = _createdAt,
            UpdatedAt = _updatedAt,
            DeletedAt = _deletedAt,
            UpdatedBy = _updatedBy
        };
}