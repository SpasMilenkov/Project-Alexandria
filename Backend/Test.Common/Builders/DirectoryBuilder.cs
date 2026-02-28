using Bogus;

namespace Test.Common.Builders;

public class DirectoryBuilder
{
    private static readonly Faker Faker = new();

    private Guid _id = Guid.NewGuid();
    private string _name = Faker.Lorem.Word();
    private Guid _ownerId = Guid.NewGuid();
    private Guid? _parentId = null;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime? _deletedAt = null;

    public DirectoryBuilder WithId(Guid id) { _id = id; return this; }
    public DirectoryBuilder WithName(string name) { _name = name; return this; }
    public DirectoryBuilder WithOwner(Guid ownerId) { _ownerId = ownerId; return this; }
    public DirectoryBuilder WithParent(Guid? parentId) { _parentId = parentId; return this; }
    public DirectoryBuilder WithCreatedAt(DateTime createdAt) { _createdAt = createdAt; return this; }
    public DirectoryBuilder WithDeletedAt(DateTime? deletedAt) { _deletedAt = deletedAt; return this; }

    public Models.Directory Build() => new()
    {
        Id = _id,
        Name = _name,
        OwnerId = _ownerId,
        ParentId = _parentId,
        CreatedAt = _createdAt,
        DeletedAt = _deletedAt,
        NormalizedName = _name.ToLowerInvariant(),
        // SearchVector is a DB-computed column — not set for in-memory test use
        Children = [],
        Files = []
    };
}
