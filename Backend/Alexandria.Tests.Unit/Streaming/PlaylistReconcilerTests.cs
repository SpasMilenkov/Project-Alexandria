using Alexandria.Dto.Files.Streaming.Playlist;
using Alexandria.Services.Streaming;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Streaming;

public class PlaylistReconcilerTests
{
    private static readonly Guid JobA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid JobB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ItemA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ItemB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static readonly DateTime DeletedAt = new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);

    private static PlaylistItemState Live(Guid itemId, Guid jobId) =>
        new(itemId, jobId, false, false, null);

    private static PlaylistItemState SystemRemoved(Guid itemId, Guid jobId) =>
        new(itemId, jobId, true, false, DeletedAt);

    private static PlaylistItemState UserRemoved(Guid itemId, Guid jobId) =>
        new(itemId, jobId, true, true, DeletedAt);

    [Fact]
    public void ComputeDiff_empty_items_adds_all_desired()
    {
        var diff = PlaylistReconciler.ComputeDiff(new HashSet<Guid> { JobA, JobB }, []);

        diff.AddJobIds.Should().BeEquivalentTo([JobA, JobB]);
        diff.ReviveItemIds.Should().BeEmpty();
        diff.RemoveItemIds.Should().BeEmpty();
    }

    [Fact]
    public void ComputeDiff_live_item_outside_desired_is_removed()
    {
        var diff = PlaylistReconciler.ComputeDiff(
            new HashSet<Guid> { JobA },
            [Live(ItemA, JobA), Live(ItemB, JobB)]);

        diff.RemoveItemIds.Should().ContainSingle().Subject.Should().Be(ItemB);
        diff.AddJobIds.Should().BeEmpty();
        diff.ReviveItemIds.Should().BeEmpty();
    }

    [Fact]
    public void ComputeDiff_system_tombstone_revives_instead_of_adding()
    {
        var diff = PlaylistReconciler.ComputeDiff(
            new HashSet<Guid> { JobA },
            [SystemRemoved(ItemA, JobA)]);

        diff.ReviveItemIds.Should().ContainSingle().Subject.Should().Be(ItemA);
        diff.AddJobIds.Should().BeEmpty();
    }

    [Fact]
    public void ComputeDiff_user_tombstone_blocks_readd()
    {
        var diff = PlaylistReconciler.ComputeDiff(
            new HashSet<Guid> { JobA },
            [UserRemoved(ItemA, JobA)]);

        diff.AddJobIds.Should().BeEmpty();
        diff.ReviveItemIds.Should().BeEmpty();
        diff.RemoveItemIds.Should().BeEmpty();
    }

    [Fact]
    public void ComputeDiff_live_row_wins_over_tombstones()
    {
        var diff = PlaylistReconciler.ComputeDiff(
            new HashSet<Guid> { JobA },
            [Live(ItemA, JobA), UserRemoved(ItemB, JobA)]);

        diff.AddJobIds.Should().BeEmpty();
        diff.ReviveItemIds.Should().BeEmpty();
        diff.RemoveItemIds.Should().BeEmpty();
    }

    [Fact]
    public void ComputeDiff_extra_live_duplicates_are_left_alone()
    {
        var duplicate = Guid.NewGuid();
        var diff = PlaylistReconciler.ComputeDiff(
            new HashSet<Guid> { JobA },
            [Live(ItemA, JobA), Live(duplicate, JobA)]);

        diff.AddJobIds.Should().BeEmpty();
        diff.ReviveItemIds.Should().BeEmpty();
        diff.RemoveItemIds.Should().BeEmpty();
    }

    [Fact]
    public void ComputeDiff_empty_desired_removes_all_live()
    {
        var diff = PlaylistReconciler.ComputeDiff(
            new HashSet<Guid>(),
            [Live(ItemA, JobA), Live(ItemB, JobB), SystemRemoved(Guid.NewGuid(), JobA)]);

        diff.RemoveItemIds.Should().BeEquivalentTo([ItemA, ItemB]);
        diff.AddJobIds.Should().BeEmpty();
        diff.ReviveItemIds.Should().BeEmpty();
    }

    [Fact]
    public void ComputeDiff_mixed_add_revive_remove()
    {
        var revivedItem = Guid.NewGuid();
        var removedItem = Guid.NewGuid();
        var diff = PlaylistReconciler.ComputeDiff(
            new HashSet<Guid> { JobA, JobB },
            [SystemRemoved(revivedItem, JobA), Live(removedItem, Guid.NewGuid())]);

        diff.AddJobIds.Should().ContainSingle().Subject.Should().Be(JobB);
        diff.ReviveItemIds.Should().ContainSingle().Subject.Should().Be(revivedItem);
        diff.RemoveItemIds.Should().ContainSingle().Subject.Should().Be(removedItem);
    }

    [Fact]
    public void ComputeDiff_user_tombstone_blocks_switched_job_for_same_file()
    {
        var file = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var filesByJob = new Dictionary<Guid, Guid> { [JobA] = file, [JobB] = file };

        var diff = PlaylistReconciler.ComputeDiff(
            new HashSet<Guid> { JobB },
            [UserRemoved(ItemA, JobA)],
            filesByJob);

        diff.AddJobIds.Should().BeEmpty();
        diff.ReviveItemIds.Should().BeEmpty();
    }

    [Fact]
    public void ComputeDiff_switched_job_for_other_file_still_adds()
    {
        var filesByJob = new Dictionary<Guid, Guid>
        {
            [JobA] = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            [JobB] = Guid.Parse("44444444-4444-4444-4444-444444444444"),
        };

        var diff = PlaylistReconciler.ComputeDiff(
            new HashSet<Guid> { JobB },
            [UserRemoved(ItemA, JobA)],
            filesByJob);

        diff.AddJobIds.Should().ContainSingle().Subject.Should().Be(JobB);
    }

    [Fact]
    public void ComputeDiff_without_file_map_keeps_job_only_precedence()
    {
        var diff = PlaylistReconciler.ComputeDiff(
            new HashSet<Guid> { JobB },
            [UserRemoved(ItemA, JobA)]);

        diff.AddJobIds.Should().ContainSingle().Subject.Should().Be(JobB);
    }
}