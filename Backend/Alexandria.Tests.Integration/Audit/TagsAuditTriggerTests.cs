using Alexandria.Data.Models.Enumerators;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Alexandria.Tests.Integration.Audit;

public class TagsAuditTriggerTests(DatabaseFixture fixture) : AuditTriggerTestBase(fixture)
{
    [Fact]
    public async Task Lifecycle_writesEveryBranch()
    {
        var genre = new TagBuilder().WithOwner(UserId).WithName("Genre").Build();
        var mood = new TagBuilder().WithOwner(UserId).WithName("Mood").Build();
        var tag = new TagBuilder().WithOwner(UserId).WithName("Jaz").WithParent(genre.Id).Build();

        await AsUserAsync(db =>
        {
            db.Tags.AddRange(genre, mood);
            return Task.CompletedTask;
        });
        await AsUserAsync(db =>
        {
            db.Tags.Add(tag);
            return Task.CompletedTask;
        });

        await AsUserAsync(async db => (await db.Tags.SingleAsync(t => t.Id == tag.Id, Ct)).Name = "Jazz");
        await AsUserAsync(async db => (await db.Tags.SingleAsync(t => t.Id == tag.Id, Ct)).Color = "#123456");
        await AsUserAsync(async db => (await db.Tags.SingleAsync(t => t.Id == tag.Id, Ct)).ParentId = mood.Id);
        await AsUserAsync(async db => (await db.Tags.SingleAsync(t => t.Id == tag.Id, Ct)).DeletedAt = DateTime.UtcNow);
        await AsUserAsync(async db => (await db.Tags.SingleAsync(t => t.Id == tag.Id, Ct)).DeletedAt = null);
        await AsUserAsync(async db => await db.Tags.Where(t => t.Id == tag.Id).ExecuteDeleteAsync(Ct));

        (await LogsAsync(AuditEventCode.TagCreated)).Should().HaveCount(3);
        Metadata(await SingleLogAsync(AuditEventCode.TagRenamed)).GetProperty("new").GetString().Should().Be("Jazz");
        Metadata(await SingleLogAsync(AuditEventCode.TagUpdated)).GetProperty("color_changed").GetBoolean()
            .Should().BeTrue();

        var reparented = Metadata(await SingleLogAsync(AuditEventCode.TagReparented));
        reparented.GetProperty("old").GetString().Should().Be("Genre");
        reparented.GetProperty("new").GetString().Should().Be("Mood");

        (await SingleLogAsync(AuditEventCode.TagSoftDeleted)).OwnerId.Should().Be(UserId);
        (await SingleLogAsync(AuditEventCode.TagRestored)).EntityId.Should().Be(tag.Id);
        (await SingleLogAsync(AuditEventCode.TagDeletedPermanently)).EntityName.Should().Be("Jazz");
    }
}
