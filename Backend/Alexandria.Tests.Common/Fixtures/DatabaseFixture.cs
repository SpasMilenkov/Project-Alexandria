using Alexandria.Data.Context;
using Alexandria.Tests.Common.TestContainers;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;
using Xunit;

namespace Alexandria.Tests.Common.Fixtures;

public class DatabaseFixture : IAsyncLifetime
{
    private readonly AlexandriaPostgresContainer _container = new();
    private Respawner _respawner = null!;

    public string ConnectionString { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.ConnectionString;

        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
        });
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);
    }

    public AlexandriaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AlexandriaDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new AlexandriaDbContext(options);
    }
}