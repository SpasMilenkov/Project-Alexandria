using Testcontainers.PostgreSql;

namespace Test.Common.TestContainers;

public sealed class AlexandriaPostgresContainer
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("alexandria_test")
        .WithUsername("test_user")
        .WithPassword("test_password")
        .WithEnvironment("POSTGRES_INITDB_ARGS", "--encoding=UTF8 --locale=en_US.UTF-8")
        .WithCleanUp(true)
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task StartAsync() => _container.StartAsync();
    public ValueTask DisposeAsync() => _container.DisposeAsync();
}