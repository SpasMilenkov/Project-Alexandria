using Alexandria.Tests.Common.Fixtures;
using Xunit;

namespace Alexandria.Tests.Integration;

[CollectionDefinition("FullStack")]
public class FullStackCollection : ICollectionFixture<AlexandriaFixture>
{
}

[CollectionDefinition("S3")]
public class S3Collection : ICollectionFixture<S3Fixture>
{
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
}