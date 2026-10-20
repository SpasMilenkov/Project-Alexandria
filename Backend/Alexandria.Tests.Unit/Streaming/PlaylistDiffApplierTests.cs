using Alexandria.Data.Models;
using Alexandria.Dto.Files.Streaming.Playlist;
using Alexandria.Repositories;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Streaming;

public class PlaylistDiffApplierTests
{
    private static readonly Guid PlaylistId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Owner = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid JobA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid JobB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ItemA = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ItemB = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTime Now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    private static PlaylistItem Item(
        Guid id,
        int position,
        Guid? jobId = null,
        bool deleted = false,
        bool removedByUser = false,
        bool addedByUser = false)
    {
        return new PlaylistItem
        {
            Id = id,
            PlaylistId = PlaylistId,
            TranspilationJobId = jobId ?? JobA,
            Position = position,
            DeletedAt = deleted ? Now.AddDays(-1) : null,
            RemovedByUser = removedByUser,
            AddedByUser = addedByUser,
        };
    }

    [Fact]
    public void Apply_appends_new_items_after_live_max_position()
    {
        var items = new List<PlaylistItem>
        {
            Item(ItemA, 0),
            Item(ItemB, 2),
            Item(Guid.NewGuid(), 5, deleted: true),
        };

        var created = PlaylistDiffApplier.Apply(
            PlaylistId,
            items,
            new PlaylistDiff([JobA, JobB], [], []),
            Owner,
            Now);

        created.Should().HaveCount(2);
        created.Select(i => i.Position).Should().Equal(3, 4);
        created.Should().OnlyContain(i =>
            i.PlaylistId == PlaylistId
            && i.CreatedAt == Now
            && i.UpdatedBy == Owner
            && i.DeletedAt == null
            && !i.AddedByUser);
        items.Should().HaveCount(5);
    }

    [Fact]
    public void Apply_revives_free_tombstone_at_original_position()
    {
        var tombstone = Item(ItemB, 1, deleted: true);
        var items = new List<PlaylistItem>
        {
            Item(ItemA, 0),
            tombstone,
        };

        var created = PlaylistDiffApplier.Apply(
            PlaylistId,
            items,
            new PlaylistDiff([], [ItemB], []),
            Owner,
            Now);

        created.Should().BeEmpty();
        tombstone.DeletedAt.Should().BeNull();
        tombstone.Position.Should().Be(1);
        tombstone.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void Apply_revives_occupied_tombstone_after_live_max()
    {
        var tombstone = Item(ItemB, 1, deleted: true);

        var items = new List<PlaylistItem>
        {
            Item(ItemA, 0),
            tombstone,
            Item(Guid.NewGuid(), 1),
        };

        PlaylistDiffApplier.Apply(
            PlaylistId,
            items,
            new PlaylistDiff([], [ItemB], []),
            Owner,
            Now);

        tombstone.DeletedAt.Should().BeNull();
        tombstone.Position.Should().Be(2);
    }

    [Fact]
    public void Apply_revive_at_free_high_position_advances_later_append()
    {
        var first = Item(ItemA, 1, deleted: true);
        var second = Item(ItemB, 1, deleted: true);

        var items = new List<PlaylistItem>
        {
            Item(Guid.NewGuid(), 0),
            first,
            second,
        };

        PlaylistDiffApplier.Apply(
            PlaylistId,
            items,
            new PlaylistDiff([], [ItemA, ItemB], []),
            Owner,
            Now);

        first.Position.Should().Be(1);
        second.Position.Should().Be(2);
    }

    [Fact]
    public void Apply_removes_live_row_without_touching_user_tombstones()
    {
        var live = Item(ItemA, 0);
        var userTombstone = Item(ItemB, 1, deleted: true, removedByUser: true);

        var items = new List<PlaylistItem>
        {
            live,
            userTombstone,
        };

        PlaylistDiffApplier.Apply(
            PlaylistId,
            items,
            new PlaylistDiff([], [], [ItemA, ItemB]),
            Owner,
            Now);

        live.DeletedAt.Should().Be(Now);
        live.UpdatedAt.Should().Be(Now);
        live.UpdatedBy.Should().Be(Owner);
        live.RemovedByUser.Should().BeFalse();

        userTombstone.DeletedAt.Should().Be(Now.AddDays(-1));
        userTombstone.RemovedByUser.Should().BeTrue();
    }

    [Fact]
    public void Apply_ignores_missing_ids_and_live_revives()
    {
        var live = Item(ItemA, 0);
        var items = new List<PlaylistItem> { live };

        var created = PlaylistDiffApplier.Apply(
            PlaylistId,
            items,
            new PlaylistDiff([], [Guid.NewGuid(), ItemA], [Guid.NewGuid()]),
            Owner,
            Now);

        created.Should().BeEmpty();
        live.Position.Should().Be(0);
        live.DeletedAt.Should().BeNull();
    }
}