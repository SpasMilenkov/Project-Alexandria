using Bogus;
using Models;
using File = Models.File;

namespace Test.Common.Builders;

public class TagBuilder
{
    private static readonly Faker Faker = new();

    private Guid _id = Guid.NewGuid();
    private string _name = Faker.Lorem.Word();
    private string _icon = Faker.Lorem.Word();
    private string _color = Faker.Internet.Color();
    private string? _description = null;
    private Guid _ownerId = Guid.NewGuid();
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime? _deletedAt = null;

    public TagBuilder WithId(Guid id) { _id = id; return this; }
    public TagBuilder WithName(string name) { _name = name; return this; }
    public TagBuilder WithIcon(string icon) { _icon = icon; return this; }
    public TagBuilder WithColor(string color) { _color = color; return this; }
    public TagBuilder WithDescription(string? description) { _description = description; return this; }
    public TagBuilder WithOwner(Guid ownerId) { _ownerId = ownerId; return this; }
    public TagBuilder WithCreatedAt(DateTime createdAt) { _createdAt = createdAt; return this; }
    public TagBuilder WithDeletedAt(DateTime? deletedAt) { _deletedAt = deletedAt; return this; }

    public Tag Build() => new()
    {
        Id = _id,
        Name = _name,
        Icon = _icon,
        Color = _color,
        Description = _description,
        OwnerId = _ownerId,
        CreatedAt = _createdAt,
        DeletedAt = _deletedAt,
        Files = []
    };
}
