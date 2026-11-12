using Alexandria.Common.Audit;
using Alexandria.Common.Config;
using Alexandria.Data.Context;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Alexandria.Tests.Integration.Audit;

/// <summary>
/// Goes through the real interceptors instead of setting the session by hand, covering the
/// writes that run outside a transaction.
/// </summary>
[Collection("Database")]
public class AuditSessionContextTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _correlationId = Guid.NewGuid();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await using var db = fixture.CreateDbContext();
        db.Users.Add(new UserBuilder().WithId(_userId).Build());
        await db.SaveChangesAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await fixture.ResetAsync();
    }

    private AlexandriaDbContext CreateContext(Guid? actor)
    {
        var accessor = new FixedAuditActor(actor, "192.168.0.9", _correlationId);

        var options = new DbContextOptionsBuilder<AlexandriaDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        return new AlexandriaDbContext(options, accessor);
    }

    private async Task<Guid> SeedDirectoryAsync()
    {
        await using var db = CreateContext(_userId);
        var directory = new DirectoryBuilder().WithOwner(_userId).WithName("Inbox").Build();
        db.Directories.Add(directory);
        await db.SaveChangesAsync(Ct);
        return directory.Id;
    }

    [Fact]
    public async Task ExecuteUpdateOutsideTransaction_keepsTheActor()
    {
        var directoryId = await SeedDirectoryAsync();

        await using (var db = CreateContext(_userId))
        {
            await db.Directories.Where(d => d.Id == directoryId)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.Name, "Renamed"), Ct);
        }

        await using var check = fixture.CreateDbContext();
        var log = await check.AuditLogs.SingleAsync(a => a.EventCode == AuditEventCode.DirectoryRenamed && a.EntityId == directoryId, Ct);

        log.UserId.Should().Be(_userId);
        log.CorrelationId.Should().Be(_correlationId);
        log.IpAddress.Should().Be("192.168.0.9");
        log.Source.Should().Be(LogSource.Trigger);
    }

    [Fact]
    public async Task SingleStatementSaveChanges_keepsTheActor()
    {
        var directoryId = await SeedDirectoryAsync();

        await using (var first = fixture.CreateDbContext())
        {
            var created = await first.AuditLogs
                .SingleAsync(a => a.EventCode == AuditEventCode.DirectoryCreated && a.EntityId == directoryId, Ct);
            created.UserId.Should().Be(_userId);
        }

        await using (var db = CreateContext(_userId))
        {
            (await db.Directories.SingleAsync(d => d.Id == directoryId, Ct)).DeletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(Ct);
        }

        await using var check = fixture.CreateDbContext();
        (await check.AuditLogs.SingleAsync(a => a.EventCode == AuditEventCode.DirectorySoftDeleted && a.EntityId == directoryId, Ct))
            .UserId.Should().Be(_userId);
    }

    [Fact]
    public async Task WorkerScopeWithoutUser_isAttributedToSystem()
    {
        var directoryId = await SeedDirectoryAsync();

        await using (var db = CreateContext(null))
        {
            await db.Directories.Where(d => d.Id == directoryId)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.Name, "Swept"), Ct);
        }

        await using var check = fixture.CreateDbContext();
        var log = await check.AuditLogs.SingleAsync(a => a.EventCode == AuditEventCode.DirectoryRenamed && a.EntityId == directoryId, Ct);

        log.UserId.Should().Be(SystemConfig.SystemId);
        log.Source.Should().Be(LogSource.System);
        log.CorrelationId.Should().Be(_correlationId);
    }

    [Fact]
    public async Task StaleSessionContext_doesNotLeakToTheNextScope()
    {
        var directoryId = await SeedDirectoryAsync();

        await using (var db = CreateContext(_userId))
        {
            await db.Directories.Where(d => d.Id == directoryId)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.Name, "First"), Ct);
        }

        // A plain context sets no session at all, as a raw write from outside the app would.
        await using (var raw = fixture.CreateDbContext())
        {
            await raw.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"Directories\" SET \"Name\" = 'Second' WHERE \"Id\" = {directoryId}", Ct);
        }

        await using var check = fixture.CreateDbContext();
        var second = await check.AuditLogs
            .Where(a => a.EventCode == AuditEventCode.DirectoryRenamed && a.EntityId == directoryId)
            .OrderByDescending(a => a.Timestamp)
            .FirstAsync(Ct);

        second.UserId.Should().Be(SystemConfig.SystemId);
    }
}
