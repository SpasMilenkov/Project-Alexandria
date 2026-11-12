using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Alexandria.Tests.Integration.Audit;

public class PreviewsAuditTriggerTests(DatabaseFixture fixture) : AuditTriggerTestBase(fixture)
{
    private static Preview Thumbnail(Guid versionId, bool isManual) => new()
    {
        Id = Guid.NewGuid(),
        MimeType = "image/webp",
        Size = 100,
        Kind = PreviewKind.Thumbnail,
        VersionId = versionId,
        ObjectKey = $"thumbnails/{Guid.NewGuid()}",
        IsManual = isManual,
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task GeneratedBySystem_writesNothing()
    {
        var file = await SeedFileAsync();

        await AsActorAsync(null, db =>
        {
            db.Previews.Add(Thumbnail(file.CurrentVersionId!.Value, isManual: false));
            return Task.CompletedTask;
        });

        (await LogsAsync(AuditEventCode.PreviewCreated)).Should().BeEmpty();
    }

    [Fact]
    public async Task CustomCoverReplacingGenerated_writesTrackCoverSet()
    {
        var file = await SeedFileAsync("song.mp3");
        var preview = Thumbnail(file.CurrentVersionId!.Value, isManual: false);

        await AsActorAsync(null, db =>
        {
            db.Previews.Add(preview);
            return Task.CompletedTask;
        });

        await AsUserAsync(async db =>
        {
            var row = await db.Previews.SingleAsync(p => p.Id == preview.Id, Ct);
            row.IsManual = true;
            row.ObjectKey = "track-covers/custom";
        });

        var log = await SingleLogAsync(AuditEventCode.TrackCoverSet);
        log.ParentEntityId.Should().Be(file.Id);
        log.EntityName.Should().Be("song.mp3");
        Metadata(log).GetProperty("replaced").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task RemovingCustomCover_writesTrackCoverRemoved()
    {
        var file = await SeedFileAsync();
        var cover = Thumbnail(file.CurrentVersionId!.Value, isManual: true);

        await AsUserAsync(db =>
        {
            db.Previews.Add(cover);
            return Task.CompletedTask;
        });
        await AsUserAsync(async db => await db.Previews.Where(p => p.Id == cover.Id).ExecuteDeleteAsync(Ct));

        (await SingleLogAsync(AuditEventCode.TrackCoverSet)).EntityId.Should().Be(cover.Id);
        (await SingleLogAsync(AuditEventCode.TrackCoverRemoved)).EntityId.Should().Be(cover.Id);
    }

    [Fact]
    public async Task UserDeletingGeneratedPreview_writesDeletedPermanently()
    {
        var file = await SeedFileAsync();
        var preview = Thumbnail(file.CurrentVersionId!.Value, isManual: false);

        await AsActorAsync(null, db =>
        {
            db.Previews.Add(preview);
            return Task.CompletedTask;
        });
        await AsUserAsync(async db => await db.Previews.Where(p => p.Id == preview.Id).ExecuteDeleteAsync(Ct));

        (await SingleLogAsync(AuditEventCode.PreviewDeletedPermanently)).ParentEntityId.Should().Be(file.Id);
    }
}
