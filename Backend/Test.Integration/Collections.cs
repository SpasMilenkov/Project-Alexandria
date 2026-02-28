using Test.Common.Fixtures;
using Xunit;

namespace Test.Integration;

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