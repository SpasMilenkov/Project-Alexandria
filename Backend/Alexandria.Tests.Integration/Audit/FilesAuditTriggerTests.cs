using Alexandria.Common.Config;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Alexandria.Tests.Integration.Audit;

public class FilesAuditTriggerTests(DatabaseFixture fixture) : AuditTriggerTestBase(fixture)
{
    [Fact]
    public async Task Insert_writesFileCreatedWithOwnerAndName()
    {
        var file = await SeedFileAsync("track.flac");

        var log = await SingleLogAsync(AuditEventCode.FileCreated);

        log.EntityId.Should().Be(file.Id);
        log.UserId.Should().Be(UserId);
        log.OwnerId.Should().Be(UserId);
        log.EntityName.Should().Be("track.flac");
        log.Source.Should().Be(LogSource.Trigger);
        log.IpAddress.Should().Be("10.1.2.3");
        log.OperationType.Should().Be(OperationType.Create);
        Metadata(log).GetProperty("mime_type").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Insert_withoutActor_fallsBackToSystem()
    {
        await AsActorAsync(null, async db =>
        {
            var co = new ContentObjectBuilder().Build();
            var version = new FileVersionBuilder().WithContentObject(co).WithCreatedBy(UserId).Build();
            var file = new FileBuilder().WithOwner(UserId).Build();
            version.File = file;
            db.AddRange(co, version, file);
            await Task.CompletedTask;
        });

        var log = await SingleLogAsync(AuditEventCode.FileCreated);

        log.UserId.Should().Be(SystemConfig.SystemId);
        log.Source.Should().Be(LogSource.System);
        log.OwnerId.Should().Be(UserId);
    }

    [Fact]
    public async Task Rename_writesOldAndNew()
    {
        var file = await SeedFileAsync("old.mp3");

        await AsUserAsync(async db =>
        {
            var entity = await db.Files.SingleAsync(f => f.Id == file.Id, Ct);
            entity.Name = "new.mp3";
        });

        var metadata = Metadata(await SingleLogAsync(AuditEventCode.FileRenamed));
        metadata.GetProperty("old").GetString().Should().Be("old.mp3");
        metadata.GetProperty("new").GetString().Should().Be("new.mp3");
    }

    [Fact]
    public async Task Move_putsDirectoryNamesInOldAndNew()
    {
        var music = await SeedDirectoryAsync("Music");
        var archive = await SeedDirectoryAsync("Archive");
        var file = await SeedFileAsync(directoryId: music.Id);

        await AsUserAsync(async db =>
        {
            var entity = await db.Files.SingleAsync(f => f.Id == file.Id, Ct);
            entity.DirectoryId = archive.Id;
        });

        var metadata = Metadata(await SingleLogAsync(AuditEventCode.FileMoved));
        metadata.GetProperty("old").GetString().Should().Be("Music");
        metadata.GetProperty("new").GetString().Should().Be("Archive");
        metadata.GetProperty("old_directory_id").GetGuid().Should().Be(music.Id);
        metadata.GetProperty("new_directory_id").GetGuid().Should().Be(archive.Id);
    }

    [Fact]
    public async Task SoftDeleteThenRestore_writesBothEvents()
    {
        var file = await SeedFileAsync();

        await AsUserAsync(async db =>
            (await db.Files.SingleAsync(f => f.Id == file.Id, Ct)).DeletedAt = DateTime.UtcNow);
        await AsUserAsync(async db =>
            (await db.Files.SingleAsync(f => f.Id == file.Id, Ct)).DeletedAt = null);

        var trashed = await SingleLogAsync(AuditEventCode.FileSoftDeleted);
        trashed.EntityId.Should().Be(file.Id);
        trashed.OperationType.Should().Be(OperationType.Delete);
        (await SingleLogAsync(AuditEventCode.FileRestored)).EntityId.Should().Be(file.Id);
    }

    [Fact]
    public async Task SwitchToExistingVersion_writesActiveVersionChanged()
    {
        var file = await SeedFileAsync();
        var firstVersionId = file.CurrentVersionId!.Value;
        var secondVersionId = Guid.NewGuid();

        // Upload a second version: the pointer moves with it, which is not a switch of its own.
        await AsUserAsync(async db =>
        {
            var co = new ContentObjectBuilder().Build();
            var version = new FileVersionBuilder().WithId(secondVersionId).WithContentObject(co)
                .WithCreatedBy(UserId).WithVersionNumber(2).WithFileId(file.Id).Build();
            db.AddRange(co, version);
            await db.SaveChangesAsync(Ct);

            (await db.Files.SingleAsync(f => f.Id == file.Id, Ct)).CurrentVersionId = secondVersionId;
        });

        (await LogsAsync(AuditEventCode.FileActiveVersionChanged)).Should().BeEmpty();

        await AsUserAsync(async db =>
            (await db.Files.SingleAsync(f => f.Id == file.Id, Ct)).CurrentVersionId = firstVersionId);

        var metadata = Metadata(await SingleLogAsync(AuditEventCode.FileActiveVersionChanged));
        metadata.GetProperty("old").GetInt32().Should().Be(2);
        metadata.GetProperty("new").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task MimeTypeChange_writesFileTypeChanged()
    {
        var file = await SeedFileAsync();

        await AsUserAsync(async db =>
            (await db.Files.SingleAsync(f => f.Id == file.Id, Ct)).MimeType = "audio/flac");

        Metadata(await SingleLogAsync(AuditEventCode.FileTypeChanged)).GetProperty("new").GetString()
            .Should().Be("audio/flac");
    }

    [Fact]
    public async Task UntrackedColumnUpdate_writesNothing()
    {
        var file = await SeedFileAsync();

        await AsUserAsync(async db =>
            (await db.Files.SingleAsync(f => f.Id == file.Id, Ct)).UpdatedAt = DateTime.UtcNow);

        await using var check = Fixture.CreateDbContext();
        var rows = await check.AuditLogs
            .CountAsync(a => a.EntityId == file.Id && a.EventCode != AuditEventCode.FileCreated, Ct);

        rows.Should().Be(0);
    }

    [Fact]
    public async Task HardDelete_writesDeletedPermanently()
    {
        var file = await SeedFileAsync("gone.mp3");

        await AsUserAsync(async db =>
        {
            await db.Files.Where(f => f.Id == file.Id).ExecuteUpdateAsync(
                s => s.SetProperty(f => f.CurrentVersionId, (Guid?)null), Ct);
            await db.FileVersions.Where(v => v.FileId == file.Id).ExecuteDeleteAsync(Ct);
            await db.Files.Where(f => f.Id == file.Id).ExecuteDeleteAsync(Ct);
        });

        var log = await SingleLogAsync(AuditEventCode.FileDeletedPermanently);
        log.EntityName.Should().Be("gone.mp3");
        log.OperationType.Should().Be(OperationType.Delete);
    }
}
