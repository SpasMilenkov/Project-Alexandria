using Bogus;
using Directory = Alexandria.Data.Models.Directory;

namespace Alexandria.Tests.Common.Builders;

public class DirectoryBuilder
{
    private static readonly Faker Faker = new();

    private Guid _id = Guid.NewGuid();
    private string _name = Faker.Lorem.Word();
    private Guid _ownerId = Guid.NewGuid();
    private Guid? _parentId = null;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime? _updatedAt = null;
    private DateTime? _deletedAt = null;
    private Guid? _updatedBy = null;

    public DirectoryBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public DirectoryBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public DirectoryBuilder WithOwner(Guid ownerId)
    {
        _ownerId = ownerId;
        return this;
    }

    public DirectoryBuilder WithParent(Guid? parentId)
    {
        _parentId = parentId;
        return this;
    }

    public DirectoryBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public DirectoryBuilder WithDeletedAt(DateTime? deletedAt)
    {
        _deletedAt = deletedAt;
        return this;
    }

    public DirectoryBuilder WithUpdatedAt(DateTime? updatedAt)
    {
        _updatedAt = updatedAt;
        return this;
    }

    public DirectoryBuilder WithUpdatedBy(Guid? updatedBy)
    {
        _updatedBy = updatedBy;
        return this;
    }

    public Directory Build() => new()
    {
        Id = _id,
        Name = _name,
        OwnerId = _ownerId,
        ParentId = _parentId,
        CreatedAt = _createdAt,
        UpdatedAt = _updatedAt,
        DeletedAt = _deletedAt,
        UpdatedBy = _updatedBy,
        NormalizedName = _name.ToLowerInvariant(),
        // SearchVector is a DB-computed column — not set for in-memory test use
        Children = [],
        Files = []
    };
}