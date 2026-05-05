using Alexandria.Data.Models;
using Bogus;

namespace Alexandria.Tests.Common.Builders;

public class UserBuilder
{
    private static readonly Faker Faker = new();

    private Guid _id = Guid.NewGuid();
    private string _name = Faker.Name.FullName();
    private string _email = Faker.Internet.Email();
    private string _userName = Faker.Internet.UserName();
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime? _deletedAt = null;

    public UserBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public UserBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public UserBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public UserBuilder WithUserName(string userName)
    {
        _userName = userName;
        return this;
    }

    public UserBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public UserBuilder WithDeletedAt(DateTime? deletedAt)
    {
        _deletedAt = deletedAt;
        return this;
    }

    public ApplicationUser Build() => new()
    {
        Id = _id,
        Name = _name,
        Email = _email,
        UserName = _userName,
        NormalizedEmail = _email.ToUpperInvariant(),
        NormalizedUserName = _userName.ToUpperInvariant(),
        CreatedAt = _createdAt,
        DeletedAt = _deletedAt,
        SecurityStamp = Guid.NewGuid().ToString(),
        ConcurrencyStamp = Guid.NewGuid().ToString(),
        EmailConfirmed = true,
    };
}