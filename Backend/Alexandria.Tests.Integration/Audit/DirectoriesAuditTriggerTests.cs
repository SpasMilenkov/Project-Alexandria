using Alexandria.Data.Models.Enumerators;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Alexandria.Tests.Integration.Audit;

public class DirectoriesAuditTriggerTests(DatabaseFixture fixture) : AuditTriggerTestBase(fixture)
{
    [Fact]
    public async Task Lifecycle_writesEveryBranch()
    {
        var parent = await SeedDirectoryAsync("Photos");
        var other = await SeedDirectoryAsync("Trips");
        var child = await SeedDirectoryAsync("2026", parent.Id);

        await AsUserAsync(async db => (await db.Directories.SingleAsync(d => d.Id == child.Id, Ct)).Name = "Paris");
        await AsUserAsync(async db => (await db.Directories.SingleAsync(d => d.Id == child.Id, Ct)).ParentId = other.Id);
        await AsUserAsync(async db =>
            (await db.Directories.SingleAsync(d => d.Id == child.Id, Ct)).DeletedAt = DateTime.UtcNow);
        await AsUserAsync(async db => (await db.Directories.SingleAsync(d => d.Id == child.Id, Ct)).DeletedAt = null);
        await AsUserAsync(async db => await db.Directories.Where(d => d.Id == child.Id).ExecuteDeleteAsync(Ct));

        (await LogsAsync(AuditEventCode.DirectoryCreated)).Should().HaveCount(3);

        var renamed = Metadata(await SingleLogAsync(AuditEventCode.DirectoryRenamed));
        renamed.GetProperty("old").GetString().Should().Be("2026");
        renamed.GetProperty("new").GetString().Should().Be("Paris");

        var moved = Metadata(await SingleLogAsync(AuditEventCode.DirectoryMoved));
        moved.GetProperty("old").GetString().Should().Be("Photos");
        moved.GetProperty("new").GetString().Should().Be("Trips");
        moved.GetProperty("new_directory_id").GetGuid().Should().Be(other.Id);

        (await SingleLogAsync(AuditEventCode.DirectorySoftDeleted)).OwnerId.Should().Be(UserId);
        (await SingleLogAsync(AuditEventCode.DirectoryRestored)).EntityId.Should().Be(child.Id);
        (await SingleLogAsync(AuditEventCode.DirectoryDeletedPermanently)).EntityName.Should().Be("Paris");
    }
}
