using Alexandria.Data.Context;
using Alexandria.Data.Context.Functions;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Directory = Alexandria.Data.Models.Directory;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Tests.Integration.Storage;

/// <summary>
/// Folder sizes are kept by Postgres triggers, so these run against a real database with every
/// migration applied. Writes go through EF, ExecuteUpdate and raw SQL alike, as the app does.
/// </summary>
[Collection("Database")]
public class DirectorySizeTriggerTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly Guid _userId = Guid.NewGuid();

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

    [Fact]
    public async Task Upload_addsCurrentVersionSizeToItsFolder()
    {
        var music = await SeedDirectoryAsync("Music");

        await SeedFileAsync(music.Id, size: 300);
        await SeedFileAsync(music.Id, size: 200);

        (await DirectFilesSizeAsync(music.Id)).Should().Be(500);
    }

    [Fact]
    public async Task RootFile_leavesFoldersUntouched()
    {
        var music = await SeedDirectoryAsync("Music");

        await SeedFileAsync(directoryId: null, size: 300);

        (await DirectFilesSizeAsync(music.Id)).Should().Be(0);
    }

    [Fact]
    public async Task TreeSize_includesNestedSubfolders()
    {
        var music = await SeedDirectoryAsync("Music");
        var albums = await SeedDirectoryAsync("Albums", music.Id);
        var live = await SeedDirectoryAsync("Live", albums.Id);

        await SeedFileAsync(music.Id, size: 100);
        await SeedFileAsync(albums.Id, size: 20);
        await SeedFileAsync(live.Id, size: 3);

        (await TreeSizeAsync(music.Id)).Should().Be(123);
        (await TreeSizeAsync(albums.Id)).Should().Be(23);
        (await DirectFilesSizeAsync(music.Id)).Should().Be(100);
    }

    [Fact]
    public async Task FileMove_shiftsSizeBetweenFolders()
    {
        var music = await SeedDirectoryAsync("Music");
        var archive = await SeedDirectoryAsync("Archive");
        var file = await SeedFileAsync(music.Id, size: 400);

        await using (var db = fixture.CreateDbContext())
        {
            await db.Files.Where(f => f.Id == file.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.DirectoryId, archive.Id), Ct);
        }

        (await DirectFilesSizeAsync(music.Id)).Should().Be(0);
        (await DirectFilesSizeAsync(archive.Id)).Should().Be(400);
    }

    [Fact]
    public async Task TrashThenRestore_dropsAndReturnsTheFile()
    {
        var music = await SeedDirectoryAsync("Music");
        var file = await SeedFileAsync(music.Id, size: 250);

        await SetFileDeletedAtAsync(file.Id, DateTime.UtcNow);
        (await DirectFilesSizeAsync(music.Id)).Should().Be(0);

        await SetFileDeletedAtAsync(file.Id, null);
        (await DirectFilesSizeAsync(music.Id)).Should().Be(250);
    }

    [Fact]
    public async Task VersionSwitch_usesTheNewCurrentVersion()
    {
        var music = await SeedDirectoryAsync("Music");
        var file = await SeedFileAsync(music.Id, size: 100);
        var secondVersionId = Guid.NewGuid();

        await using (var db = fixture.CreateDbContext())
        {
            var co = new ContentObjectBuilder().Build();
            var version = new FileVersionBuilder().WithId(secondVersionId).WithContentObject(co)
                .WithCreatedBy(_userId).WithVersionNumber(2).WithSize(900).WithFileId(file.Id).Build();
            db.AddRange(co, version);
            await db.SaveChangesAsync(Ct);

            await db.Files.Where(f => f.Id == file.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.CurrentVersionId, secondVersionId), Ct);
        }

        (await DirectFilesSizeAsync(music.Id)).Should().Be(900);
    }

    [Fact]
    public async Task FolderMove_carriesItsSubtreeToTheNewParent()
    {
        var music = await SeedDirectoryAsync("Music");
        var archive = await SeedDirectoryAsync("Archive");
        var albums = await SeedDirectoryAsync("Albums", music.Id);
        await SeedFileAsync(albums.Id, size: 700);

        await using (var db = fixture.CreateDbContext())
        {
            await db.Directories.Where(d => d.Id == albums.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.ParentId, archive.Id), Ct);
        }

        (await TreeSizeAsync(music.Id)).Should().Be(0);
        (await TreeSizeAsync(archive.Id)).Should().Be(700);
    }

    [Fact]
    public async Task TrashedSubfolder_dropsOutOfTheParentTotal()
    {
        var music = await SeedDirectoryAsync("Music");
        var albums = await SeedDirectoryAsync("Albums", music.Id);
        await SeedFileAsync(music.Id, size: 10);
        await SeedFileAsync(albums.Id, size: 90);

        await using (var db = fixture.CreateDbContext())
        {
            await db.Directories.Where(d => d.Id == albums.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.DeletedAt, DateTime.UtcNow), Ct);
        }

        (await TreeSizeAsync(music.Id)).Should().Be(10);
        (await TreeSizeAsync(albums.Id)).Should().Be(0);
    }

    [Fact]
    public async Task BulkTrashInOneStatement_emptiesEveryTouchedFolder()
    {
        var music = await SeedDirectoryAsync("Music");
        var albums = await SeedDirectoryAsync("Albums", music.Id);
        await SeedFileAsync(music.Id, size: 10);
        await SeedFileAsync(music.Id, size: 20);
        await SeedFileAsync(albums.Id, size: 30);

        await using (var db = fixture.CreateDbContext())
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "Files" SET "DeletedAt" = NOW() WHERE "OwnerId" = {_userId}""", Ct);
        }

        (await DirectFilesSizeAsync(music.Id)).Should().Be(0);
        (await DirectFilesSizeAsync(albums.Id)).Should().Be(0);
    }

    [Fact]
    public async Task HardDeleteOfLiveFile_removesItsSize()
    {
        var music = await SeedDirectoryAsync("Music");
        var file = await SeedFileAsync(music.Id, size: 60);
        await SeedFileAsync(music.Id, size: 40);

        await using (var db = fixture.CreateDbContext())
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""DELETE FROM "Files" WHERE "Id" = {file.Id}""", Ct);
        }

        (await DirectFilesSizeAsync(music.Id)).Should().Be(40);
    }

    [Fact]
    public async Task ConcurrentUploadsIntoOneFolder_bothCount()
    {
        var music = await SeedDirectoryAsync("Music");

        await using var first = fixture.CreateDbContext();
        await using var firstTransaction = await first.Database.BeginTransactionAsync(Ct);
        await AddFileAsync(first, music.Id, size: 111);

        // The second upload sums the folder while the first is still uncommitted. Without the
        // lock-then-sum order its write would overwrite the first file's bytes.
        var second = Task.Run(async () =>
        {
            await using var db = fixture.CreateDbContext();
            await using var transaction = await db.Database.BeginTransactionAsync(Ct);
            await AddFileAsync(db, music.Id, size: 222);
            await transaction.CommitAsync(Ct);
        }, Ct);

        await WaitForBlockedBackendAsync();
        await firstTransaction.CommitAsync(Ct);
        await second;

        (await DirectFilesSizeAsync(music.Id)).Should().Be(333);
    }

    [Fact]
    public async Task UnrelatedFileUpdate_keepsTheSize()
    {
        var music = await SeedDirectoryAsync("Music");
        var file = await SeedFileAsync(music.Id, size: 80);

        await using (var db = fixture.CreateDbContext())
        {
            await db.Files.Where(f => f.Id == file.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.Name, "renamed.mp3"), Ct);
        }

        (await DirectFilesSizeAsync(music.Id)).Should().Be(80);
    }

    private async Task<Directory> SeedDirectoryAsync(string name, Guid? parentId = null)
    {
        await using var db = fixture.CreateDbContext();
        var directory = new DirectoryBuilder().WithOwner(_userId).WithName(name).WithParent(parentId).Build();

        db.Directories.Add(directory);
        await db.SaveChangesAsync(Ct);

        return directory;
    }

    private async Task<File> SeedFileAsync(Guid? directoryId, long size)
    {
        await using var db = fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync(Ct);

        var file = await AddFileAsync(db, directoryId, size);
        await transaction.CommitAsync(Ct);

        return file;
    }

    // Mirrors the upload path: the file row lands first, then its current version is set.
    private async Task<File> AddFileAsync(AlexandriaDbContext db, Guid? directoryId, long size)
    {
        var co = new ContentObjectBuilder().Build();
        var version = new FileVersionBuilder().WithContentObject(co).WithCreatedBy(_userId).WithSize(size).Build();
        var file = new FileBuilder().WithOwner(_userId).WithDirectory(directoryId).Build();
        version.File = file;

        db.AddRange(co, version, file);
        await db.SaveChangesAsync(Ct);

        file.CurrentVersionId = version.Id;
        await db.SaveChangesAsync(Ct);

        return file;
    }

    private async Task SetFileDeletedAtAsync(Guid fileId, DateTime? deletedAt)
    {
        await using var db = fixture.CreateDbContext();
        await db.Files.Where(f => f.Id == fileId)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.DeletedAt, deletedAt), Ct);
    }

    private async Task<long> DirectFilesSizeAsync(Guid directoryId)
    {
        await using var db = fixture.CreateDbContext();

        return await db.Directories.AsNoTracking()
            .Where(d => d.Id == directoryId)
            .Select(d => d.DirectFilesSize)
            .SingleAsync(Ct);
    }

    private async Task<long> TreeSizeAsync(Guid directoryId)
    {
        await using var db = fixture.CreateDbContext();

        return await db.Directories.AsNoTracking()
            .Where(d => d.Id == directoryId)
            .Select(d => DatabaseFunctions.DirectoryTreeSize(d.Id))
            .SingleAsync(Ct);
    }

    private async Task WaitForBlockedBackendAsync()
    {
        await using var db = fixture.CreateDbContext();
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < deadline)
        {
            var blocked = await db.Database
                .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE wait_event_type = 'Lock'")
                .SingleAsync(Ct);

            if (blocked > 0)
                return;

            await Task.Delay(50, Ct);
        }

        throw new TimeoutException("The second upload never waited on the folder lock.");
    }
}
