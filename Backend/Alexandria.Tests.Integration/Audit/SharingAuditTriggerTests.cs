using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Alexandria.Tests.Integration.Audit;

public class SharingAuditTriggerTests(DatabaseFixture fixture) : AuditTriggerTestBase(fixture)
{
    [Fact]
    public async Task ShareLink_lifecycle_neverStoresTheToken()
    {
        var file = await SeedFileAsync("holiday.jpg");
        var link = new SignedUrl
        {
            Id = Guid.NewGuid(),
            FileId = file.Id,
            Token = "secret-bearer-token",
            CreatorId = UserId,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            MaxAccessCount = 1,
            CreatedAt = DateTime.UtcNow,
        };

        await AsUserAsync(db =>
        {
            db.SignedUrls.Add(link);
            return Task.CompletedTask;
        });

        // An anonymous download: no user behind the request.
        await AsActorAsync(null, async db => await db.SignedUrls.Where(s => s.Id == link.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AccessCount, x => x.AccessCount + 1), Ct));

        await AsUserAsync(async db =>
            (await db.SignedUrls.SingleAsync(s => s.Id == link.Id, Ct)).DeletedAt = DateTime.UtcNow);

        var created = await SingleLogAsync(AuditEventCode.ShareLinkCreated);
        created.ParentEntityId.Should().Be(file.Id);
        created.EntityName.Should().Be("holiday.jpg");

        var accessed = await SingleLogAsync(AuditEventCode.ShareLinkAccessed);
        accessed.OwnerId.Should().Be(UserId);
        accessed.Source.Should().Be(LogSource.System);
        accessed.IpAddress.Should().Be("10.1.2.3");
        Metadata(accessed).GetProperty("exhausted").GetBoolean().Should().BeTrue();

        (await SingleLogAsync(AuditEventCode.ShareLinkRevoked)).EntityId.Should().Be(link.Id);

        (await AnyMetadataContainsAsync("secret-bearer-token")).Should().BeFalse();
    }

    [Fact]
    public async Task FileTags_userChangesAreAudited_systemTaggingIsNot()
    {
        var file = await SeedFileAsync("track.mp3");
        var userTag = new TagBuilder().WithOwner(UserId).WithName("Favourites").Build();
        var autoTag = new TagBuilder().WithOwner(UserId).WithName("Rock").Build();

        await AsUserAsync(db =>
        {
            db.Tags.AddRange(userTag, autoTag);
            return Task.CompletedTask;
        });

        await AsUserAsync(db =>
        {
            db.FileTags.Add(new FileTag { Id = Guid.NewGuid(), FileId = file.Id, TagId = userTag.Id, Source = TagSource.User });
            return Task.CompletedTask;
        });
        await AsActorAsync(null, db =>
        {
            db.FileTags.Add(new FileTag
            {
                Id = Guid.NewGuid(), FileId = file.Id, TagId = autoTag.Id, Source = TagSource.Auto, Confidence = 0.9
            });
            return Task.CompletedTask;
        });
        await AsUserAsync(async db =>
            await db.FileTags.Where(ft => ft.TagId == userTag.Id).ExecuteDeleteAsync(Ct));

        var applied = await SingleLogAsync(AuditEventCode.FileTagApplied);
        applied.EntityId.Should().Be(userTag.Id);
        applied.ParentEntityId.Should().Be(file.Id);
        applied.EntityName.Should().Be("Favourites");
        (await SingleLogAsync(AuditEventCode.FileTagRemoved)).EntityId.Should().Be(userTag.Id);
    }
}
