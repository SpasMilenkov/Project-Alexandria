using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Infrastructure;
using Alexandria.Infrastructure.DocumentWorker;
using Alexandria.Services.Storage;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fakes;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Alexandria.Tests.Integration.Audit;

/// <summary>
/// A bulk edit runs every file through one context. When one file fails, rolling back has to drop
/// what that file staged, or the next file's save writes the failed edit and its audit row anyway.
/// </summary>
public class BulkMetadataRollbackTests(DatabaseFixture fixture) : AuditTriggerTestBase(fixture)
{
    [Theory]
    [InlineData("true")]
    [InlineData("nextval('bulk_rollback_seq') = 1")]
    public async Task OneFileFailing_leavesItUntouched_andTheRestStillUpdate(string failWhen)
    {
        var ids = new List<Guid>();
        foreach (var name in new[] { "a.mp3", "b.mp3", "c.mp3" })
            ids.Add((await SeedTrackAsync(name)).Id);

        await using var provider = BuildServices();

        // Fails every UPDATE of the first file, or only the first one, to cover both a lasting
        // data problem and a passing one such as a deadlock.
        await ExecuteAsync($"""
            CREATE SEQUENCE bulk_rollback_seq;
            CREATE FUNCTION bulk_rollback_fail() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF {failWhen} THEN RAISE EXCEPTION 'injected failure'; END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER bulk_rollback_fail BEFORE UPDATE ON "Files" FOR EACH ROW
                WHEN (OLD."Id" = '{ids[0]}') EXECUTE FUNCTION bulk_rollback_fail();
            """);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var sp = scope.ServiceProvider;
            var service = new FileService(sp.GetRequiredService<IUnitOfWork>(), null!, new FakePublisherService(),
                sp.GetRequiredService<IAuditWriter>(), NullLogger<FileService>.Instance);

            var results = await service.BulkUpdateFileMetadataAsync(ids.ToArray(), UserId, newTitle: "new title",
                ct: Ct);

            results.Select(r => r.Success).Should().Equal(false, true, true);
        }
        finally
        {
            await ExecuteAsync("""
                DROP TRIGGER bulk_rollback_fail ON "Files";
                DROP FUNCTION bulk_rollback_fail();
                DROP SEQUENCE bulk_rollback_seq;
                """);
        }

        await using var db = Fixture.CreateDbContext();
        var titles = await db.MediaMetadata.Where(m => ids.Contains(m.FileId))
            .ToDictionaryAsync(m => m.FileId, m => m.Title, Ct);
        var edits = await db.AuditLogs
            .Where(a => a.EventCode == AuditEventCode.MediaMetadataEdited && ids.Contains(a.EntityId!.Value))
            .Select(a => a.EntityId!.Value)
            .ToListAsync(Ct);

        titles[ids[0]].Should().Be("old title");
        titles[ids[1]].Should().Be("new title");
        titles[ids[2]].Should().Be("new title");
        edits.Should().BeEquivalentTo([ids[1], ids[2]]);
    }

    private ServiceProvider BuildServices()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AlexandriaPostgres"] = Fixture.ConnectionString,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddWorkerDatabase(config);
        services.AddServices();

        return services.BuildServiceProvider();
    }

    private async Task<Data.Models.File> SeedTrackAsync(string name)
    {
        var file = await SeedFileAsync(name);

        await using var db = Fixture.CreateDbContext();
        db.MediaMetadata.Add(new MediaMetadata
        {
            Id = Guid.NewGuid(),
            FileId = file.Id,
            Title = "old title",
            Duration = 60,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(Ct);

        return file;
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var db = Fixture.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(sql, Ct);
    }
}
