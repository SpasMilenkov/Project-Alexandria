using System.Text.Json;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Tests.Integration.Audit;

/// <summary>
/// Trigger tests run against a real Postgres with every migration applied, so a migration that
/// breaks a trigger fails here instead of silently dropping rows in production.
/// </summary>
[Collection("Database")]
public abstract class AuditTriggerTestBase(DatabaseFixture fixture) : IAsyncLifetime
{
    protected readonly DatabaseFixture Fixture = fixture;
    protected readonly Guid UserId = Guid.NewGuid();
    protected readonly Guid CorrelationId = Guid.NewGuid();

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await using var db = Fixture.CreateDbContext();
        db.Users.Add(new UserBuilder().WithId(UserId).Build());
        await db.SaveChangesAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.ResetAsync();
    }

    /// <summary>
    /// Runs <paramref name="work"/> in one transaction whose audit session names
    /// <paramref name="actor"/>; null behaves like a worker with no user behind it.
    /// </summary>
    protected async Task AsActorAsync(Guid? actor, Func<AlexandriaDbContext, Task> work)
    {
        await using var db = Fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync(Ct);

        var actorText = actor?.ToString() ?? "";
        var correlationText = CorrelationId.ToString();

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.current_user_id', {actorText}, true), set_config('app.current_ip', '10.1.2.3', true), set_config('app.correlation_id', {correlationText}, true)",
            Ct);

        await work(db);
        await db.SaveChangesAsync(Ct);
        await transaction.CommitAsync(Ct);
    }

    protected Task AsUserAsync(Func<AlexandriaDbContext, Task> work) => AsActorAsync(UserId, work);

    protected async Task<List<AuditLog>> LogsAsync(AuditEventCode code)
    {
        await using var db = Fixture.CreateDbContext();

        // Filtering on this test's correlation id keeps rows from other tests out.
        return await db.AuditLogs.AsNoTracking()
            .Where(a => a.EventCode == code && a.CorrelationId == CorrelationId)
            .OrderBy(a => a.Timestamp)
            .ToListAsync(Ct);
    }

    protected async Task<AuditLog> SingleLogAsync(AuditEventCode code)
    {
        var logs = await LogsAsync(code);

        if (logs.Count != 1)
            throw new InvalidOperationException($"Expected one {code} row, found {logs.Count}");

        return logs[0];
    }

    /// <summary>True when any audit row's metadata contains <paramref name="text"/>.</summary>
    protected async Task<bool> AnyMetadataContainsAsync(string text)
    {
        await using var db = Fixture.CreateDbContext();
        var pattern = $"%{text}%";

        var matches = await db.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM \"AuditLogs\" WHERE \"MetadataJson\"::text LIKE {pattern}")
            .SingleAsync(Ct);

        return matches > 0;
    }

    protected static JsonElement Metadata(AuditLog log)
    {
        using var document = JsonDocument.Parse(log.MetadataJson ?? "{}");
        return document.RootElement.Clone();
    }

    protected async Task<File> SeedFileAsync(string name = "song.mp3", Guid? directoryId = null)
    {
        File file = null!;

        await AsUserAsync(async db =>
        {
            var co = new ContentObjectBuilder().Build();
            var version = new FileVersionBuilder().WithContentObject(co).WithCreatedBy(UserId).Build();
            file = new FileBuilder().WithOwner(UserId).WithName(name).WithDirectory(directoryId).Build();
            version.File = file;

            db.ContentObjects.Add(co);
            db.FileVersions.Add(version);
            db.Files.Add(file);
            await db.SaveChangesAsync(Ct);

            file.CurrentVersionId = version.Id;
        });

        return file;
    }

    protected async Task<Data.Models.Directory> SeedDirectoryAsync(string name, Guid? parentId = null)
    {
        Data.Models.Directory directory = null!;

        await AsUserAsync(db =>
        {
            directory = new DirectoryBuilder().WithOwner(UserId).WithName(name).WithParent(parentId).Build();
            db.Directories.Add(directory);
            return Task.CompletedTask;
        });

        return directory;
    }
}
