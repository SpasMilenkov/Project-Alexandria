using Alexandria.Workers.Playlist.Queueing;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Streaming;

public class PlaylistSyncBufferTests
{
    private static readonly Guid Owner = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public async Task duplicate_file_folds_delivery_tags_into_one_batch_item()
    {
        var fileId = Guid.NewGuid();
        var buffer = new PlaylistSyncBuffer();

        buffer.TryEnqueue(Owner, fileId, 1).Should().Be(EnqueueResult.Added);
        buffer.TryEnqueue(Owner, fileId, 2).Should().Be(EnqueueResult.Duplicate);

        var batch = await buffer.ReadBatchAsync(10, TimeSpan.Zero,
            TestContext.Current.CancellationToken);

        batch.Should().ContainSingle();
        batch[0].FileId.Should().Be(fileId);
        batch[0].DeliveryTags.Should().BeEquivalentTo([1UL, 2UL]);
    }

    [Fact]
    public async Task same_file_for_different_owners_produces_separate_batch_items()
    {
        var fileId = Guid.NewGuid();
        var otherOwner = Guid.NewGuid();
        var buffer = new PlaylistSyncBuffer();

        buffer.TryEnqueue(Owner, fileId, 1).Should().Be(EnqueueResult.Added);
        buffer.TryEnqueue(otherOwner, fileId, 2).Should().Be(EnqueueResult.Added);

        var batch = await buffer.ReadBatchAsync(10, TimeSpan.Zero,
            TestContext.Current.CancellationToken);

        batch.Should().HaveCount(2);
        batch.Select(item => item.OwnerId).Should().BeEquivalentTo([Owner, otherOwner]);
    }

    [Fact]
    public async Task batch_stops_at_configured_max_size()
    {
        var buffer = new PlaylistSyncBuffer();

        for (var index = 0; index < 3; index++)
            buffer.TryEnqueue(Owner, Guid.NewGuid(), (ulong)index).Should().Be(EnqueueResult.Added);

        var batch = await buffer.ReadBatchAsync(2, TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken);

        batch.Should().HaveCount(2);
    }

    [Fact]
    public async Task file_can_reenter_a_new_batch_after_previous_batch_is_read()
    {
        var fileId = Guid.NewGuid();
        var buffer = new PlaylistSyncBuffer();

        buffer.TryEnqueue(Owner, fileId, 1).Should().Be(EnqueueResult.Added);
        var first = await buffer.ReadBatchAsync(10, TimeSpan.Zero,
            TestContext.Current.CancellationToken);

        buffer.TryEnqueue(Owner, fileId, 2).Should().Be(EnqueueResult.Added);
        var second = await buffer.ReadBatchAsync(10, TimeSpan.Zero,
            TestContext.Current.CancellationToken);

        first.Should().ContainSingle();
        second.Should().ContainSingle();
        second[0].DeliveryTags.Should().BeEquivalentTo([2UL]);
    }

    [Fact]
    public async Task full_buffer_returns_buffer_full_without_leaving_pending_entry()
    {
        var buffer = new PlaylistSyncBuffer(capacity: 1);
        var firstFile = Guid.NewGuid();
        var secondFile = Guid.NewGuid();

        buffer.TryEnqueue(Owner, firstFile, 1).Should().Be(EnqueueResult.Added);
        buffer.TryEnqueue(Owner, secondFile, 2).Should().Be(EnqueueResult.BufferFull);

        var batch = await buffer.ReadBatchAsync(10, TimeSpan.Zero,
            TestContext.Current.CancellationToken);

        batch.Should().ContainSingle();
        batch[0].FileId.Should().Be(firstFile);
        buffer.TryEnqueue(Owner, secondFile, 3).Should().Be(EnqueueResult.Added);
    }
}