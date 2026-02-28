// Tests.Common/Fixtures/AlexandriaFixture.cs

using Data.Context;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;
using Test.Common.TestContainers;
using Xunit;

namespace Test.Common.Fixtures;

public class AlexandriaFixture : IAsyncLifetime
{
    private readonly AlexandriaPostgresContainer _postgres = new();
    private readonly AlexandriaRabbitMqContainer _rabbitMq = new();
    private readonly GarageContainer _garage = new();
    private Respawner _respawner = null!;

    public string PostgresConnectionString { get; private set; } = null!;

    public string RabbitMqHost { get; private set; } = null!;
    public int RabbitMqPort { get; private set; }
    public string RabbitMqUsername => _rabbitMq.Username;
    public string RabbitMqPassword => _rabbitMq.Password;
    public string RabbitMqVirtualHost => _rabbitMq.VirtualHost;

    public string GarageEndpoint { get; private set; } = null!;
    public string GarageAccessKey { get; private set; } = null!;
    public string GarageSecretKey { get; private set; } = null!;
    public string GarageRegion => _garage.Region;

    public const string TestJwtSecret = "alexandria-test-secret-key-long-enough-for-hmac";
    public const string TestJwtIssuer = "alexandria-test";
    public const string TestJwtAudience = "alexandria-test";


    public async ValueTask InitializeAsync()
    {
        // All three start in parallel — no reason to wait for each sequentially
        await Task.WhenAll(
            _postgres.StartAsync(),
            _rabbitMq.StartAsync(),
            _garage.StartAsync()
        );

        PostgresConnectionString = _postgres.ConnectionString;

        RabbitMqHost = _rabbitMq.Host;
        RabbitMqPort = _rabbitMq.Port;

        GarageEndpoint = _garage.Endpoint;
        GarageAccessKey = _garage.AccessKey;
        GarageSecretKey = _garage.SecretKey;

        // Migrations run after containers are up
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();

        // Respawner inspects schema after migrations
        await using var conn = new NpgsqlConnection(PostgresConnectionString);
        await conn.OpenAsync();

        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
        });
    }

    public async ValueTask DisposeAsync()
    {
        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _rabbitMq.DisposeAsync().AsTask(),
            _garage.DisposeAsync().AsTask()
        );
    }

    public async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(PostgresConnectionString);
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);
    }

    public AlexandriaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AlexandriaDbContext>()
            .UseNpgsql(PostgresConnectionString)
            .Options;

        return new AlexandriaDbContext(options);
    }

    /// <summary>
    /// Returns configuration overrides for use in WebApplicationFactory
    /// or any IServiceCollection that needs to point at the test containers.
    /// </summary>
    public Dictionary<string, string?> GetConfigurationOverrides() => new()
    {
        ["ConnectionStrings:AlexandriaPostgres"] = PostgresConnectionString,

        ["RabbitMQ:HostName"] = RabbitMqHost,
        ["RabbitMQ:Port"] = RabbitMqPort.ToString(),
        ["RabbitMQ:UserName"] = RabbitMqUsername,
        ["RabbitMQ:Password"] = RabbitMqPassword,
        ["RabbitMQ:VirtualHost"] = RabbitMqVirtualHost,

        ["S3Storage:Endpoint"] = GarageEndpoint,
        ["S3Storage:AccessKey"] = GarageAccessKey,
        ["S3Storage:SecretKey"] = GarageSecretKey,
        ["S3Storage:Region"] = GarageRegion,

        ["Jwt:Secret"] = TestJwtSecret,
        ["Jwt:Issuer"] = TestJwtIssuer,
        ["Jwt:Audience"] = TestJwtAudience,

        ["Admin:Email"] = "admin@alexandria-test.com",
        ["Admin:Name"] = "TestAdmin",
        ["Admin:Password"] = "Admin1234!",

        // Infrastructure health checks — all backed by Testcontainers
        ["HealthChecks:Database:Enabled"] = "false",
        ["HealthChecks:Storage:Enabled"] = "false",
        ["HealthChecks:MessageBroker:Enabled"] = "false",
        // Workers don't exist in the test environment
        ["HealthChecks:Workers:0:Enabled"] = "false",
        ["HealthChecks:Workers:1:Enabled"] = "false",
    };
}