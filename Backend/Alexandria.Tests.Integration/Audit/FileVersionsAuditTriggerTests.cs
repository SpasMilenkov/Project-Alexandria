using Alexandria.Data.Models.Enumerators;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Alexandria.Tests.Integration.Audit;

public class FileVersionsAuditTriggerTests(DatabaseFixture fixture) : AuditTriggerTestBase(fixture)
{
    [Fact]
    public async Task Insert_carriesParentFileOwnerAndName()
    {
        var file = await SeedFileAsync("report.pdf");

        var log = await SingleLogAsync(AuditEventCode.FileVersionCreated);

        log.EntityId.Should().Be(file.CurrentVersionId);
        log.ParentEntityId.Should().Be(file.Id);
        log.OwnerId.Should().Be(UserId);
        log.EntityName.Should().Be("report.pdf");
        Metadata(log).GetProperty("version_number").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task SoftDeleteRestoreAndDelete_writeEachEvent()
    {
        var file = await SeedFileAsync();
        var versionId = file.CurrentVersionId!.Value;

        await AsUserAsync(async db =>
            (await db.FileVersions.SingleAsync(v => v.Id == versionId, Ct)).DeletedAt = DateTime.UtcNow);
        await AsUserAsync(async db =>
            (await db.FileVersions.SingleAsync(v => v.Id == versionId, Ct)).DeletedAt = null);
        await AsUserAsync(async db =>
        {
            await db.Files.Where(f => f.Id == file.Id).ExecuteUpdateAsync(
                s => s.SetProperty(f => f.CurrentVersionId, (Guid?)null), Ct);
            await db.FileVersions.Where(v => v.Id == versionId).ExecuteDeleteAsync(Ct);
        });

        (await SingleLogAsync(AuditEventCode.FileVersionSoftDeleted)).ParentEntityId.Should().Be(file.Id);
        (await SingleLogAsync(AuditEventCode.FileVersionRestored)).ParentEntityId.Should().Be(file.Id);
        (await SingleLogAsync(AuditEventCode.FileVersionDeletedPermanently)).EntityName.Should().Be(file.Name);
    }

    [Fact]
    public async Task CascadeFromFileHardDelete_keepsTheOwner()
    {
        var file = await SeedFileAsync("secret.pdf");

        await AsActorAsync(null, db => db.Files.Where(f => f.Id == file.Id).ExecuteDeleteAsync(Ct));

        var log = await SingleLogAsync(AuditEventCode.FileVersionDeletedPermanently);

        log.ParentEntityId.Should().Be(file.Id);
        log.OwnerId.Should().Be(UserId);
    }
}
