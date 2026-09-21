using System.Collections.Immutable;
using Alexandria.Common.Exceptions.Streaming;
using Alexandria.Dto.Files.Streaming.Shuffle;
using Alexandria.Services.Streaming.Shuffle;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Alexandria.Tests.Unit.Streaming;

public class ShuffleSessionStoreTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private sealed class TestTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now += delta;
    }

    private static ShuffleSessionStore CreateStore(TestTimeProvider time) =>
        new(time, Substitute.For<ILogger<ShuffleSessionStore>>());

    private static ShuffleSessionState MakeState(
        Guid ownerId, Guid requestId, string fingerprint, int count, DateTime? createdAt = null)
    {
        var order = Enumerable.Range(0, count)
            .Select(i => new PlaybackSourceEntryRef
            {
                FileId = Guid.NewGuid(),
                PlaylistItemId = null,
                TranspilationJobId = Guid.NewGuid(),
            })
            .ToImmutableArray();
        return new ShuffleSessionState(
            Guid.NewGuid(),
            ownerId,
            new PlaybackSourceDto { IsVideo = false, PlaylistId = null },
            createdAt ?? Start.UtcDateTime,
            1,
            fingerprint,
            requestId,
            null,
            order);
    }

    [Fact]
    public async Task GetOrCreate_concurrent_same_key_runs_factory_once()
    {
        var time = new TestTimeProvider(Start);
        using var store = CreateStore(time);
        var owner = Guid.NewGuid();
        var request = Guid.NewGuid();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runs = 0;

        async Task<ShuffleSessionState> Factory()
        {
            Interlocked.Increment(ref runs);
            await gate.Task;
            return MakeState(owner, request, "fp", 3);
        }

        var ct = TestContext.Current.CancellationToken;
        var first = store.GetOrCreateAsync(owner, request, "fp", Factory, ct);
        var second = store.GetOrCreateAsync(owner, request, "fp", Factory, ct);
        gate.SetResult();

        var results = await Task.WhenAll(first, second);
        runs.Should().Be(1);
        results[0].SessionId.Should().Be(results[1].SessionId);
    }

    [Fact]
    public async Task GetOrCreate_different_payload_conflicts()
    {
        var time = new TestTimeProvider(Start);
        using var store = CreateStore(time);
        var owner = Guid.NewGuid();
        var request = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        await store.GetOrCreateAsync(owner, request, "fp-a",
            () => Task.FromResult(MakeState(owner, request, "fp-a", 2)), ct);

        var act = () => store.GetOrCreateAsync(owner, request, "fp-b",
            () => Task.FromResult(MakeState(owner, request, "fp-b", 2)), ct);

        await act.Should().ThrowAsync<ShuffleRequestConflictException>();
    }

    [Fact]
    public async Task GetOrCreate_failed_creation_can_retry()
    {
        var time = new TestTimeProvider(Start);
        using var store = CreateStore(time);
        var owner = Guid.NewGuid();
        var request = Guid.NewGuid();
        var attempts = 0;
        var ct = TestContext.Current.CancellationToken;

        Task<ShuffleSessionState> Flaky()
        {
            attempts++;
            return attempts == 1
                ? Task.FromException<ShuffleSessionState>(new InvalidOperationException("boom"))
                : Task.FromResult(MakeState(owner, request, "fp", 2));
        }

        Func<Task> act = () => store.GetOrCreateAsync(owner, request, "fp", Flaky, ct);
        await act.Should().ThrowAsync<InvalidOperationException>();

        var state = await store.GetOrCreateAsync(owner, request, "fp", Flaky, ct);
        attempts.Should().Be(2);

        var replayed = await store.GetOrCreateAsync(owner, request, "fp", Flaky, ct);
        replayed.SessionId.Should().Be(state.SessionId);
        attempts.Should().Be(2);
    }

    [Fact]
    public async Task GetOrCreate_cancelled_waiter_does_not_poison_retry()
    {
        var time = new TestTimeProvider(Start);
        using var store = CreateStore(time);
        var owner = Guid.NewGuid();
        var request = Guid.NewGuid();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runs = 0;

        async Task<ShuffleSessionState> Factory()
        {
            Interlocked.Increment(ref runs);
            await gate.Task;
            return MakeState(owner, request, "fp", 2);
        }

        using var cts = new CancellationTokenSource();
        var waiting = store.GetOrCreateAsync(owner, request, "fp", Factory, cts.Token);
        await Task.Delay(50);
        await cts.CancelAsync();
        Func<Task> act = () => waiting;
        await act.Should().ThrowAsync<OperationCanceledException>();

        gate.SetResult();
        var state = await store.GetOrCreateAsync(
            owner, request, "fp", Factory, TestContext.Current.CancellationToken);

        runs.Should().Be(1);
        state.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetOrCreate_same_request_different_owners_are_isolated()
    {
        var time = new TestTimeProvider(Start);
        using var store = CreateStore(time);
        var request = Guid.NewGuid();
        var firstOwner = Guid.NewGuid();
        var secondOwner = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        var first = await store.GetOrCreateAsync(firstOwner, request, "fp",
            () => Task.FromResult(MakeState(firstOwner, request, "fp", 2)), ct);
        var second = await store.GetOrCreateAsync(secondOwner, request, "fp",
            () => Task.FromResult(MakeState(secondOwner, request, "fp", 2)), ct);

        first.SessionId.Should().NotBe(second.SessionId);
        store.TryGet(first.SessionId, secondOwner, out _, out _).Should().BeFalse();
        store.TryGet(first.SessionId, firstOwner, out _, out _).Should().BeTrue();
    }

    [Fact]
    public async Task Publish_ninth_session_evicts_least_recently_used()
    {
        var time = new TestTimeProvider(Start);
        using var store = CreateStore(time);
        var owner = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;
        var ids = new List<Guid>();

        for (var i = 0; i < 8; i++)
        {
            var state = await store.GetOrCreateAsync(owner, Guid.NewGuid(), $"fp-{i}",
                () => Task.FromResult(MakeState(owner, Guid.NewGuid(), $"fp-{i}", 1)), ct);
            ids.Add(state.SessionId);
            time.Advance(TimeSpan.FromMinutes(1));
        }

        var ninth = await store.GetOrCreateAsync(owner, Guid.NewGuid(), "fp-8",
            () => Task.FromResult(MakeState(owner, Guid.NewGuid(), "fp-8", 1)), ct);

        store.TryGet(ids[0], owner, out _, out _).Should().BeFalse();
        foreach (var id in ids.Skip(1))
            store.TryGet(id, owner, out _, out _).Should().BeTrue();
        store.TryGet(ninth.SessionId, owner, out _, out _).Should().BeTrue();
    }

    [Fact]
    public async Task Publish_ninth_session_with_tied_access_breaks_tie_by_session_id()
    {
        var time = new TestTimeProvider(Start);
        using var store = CreateStore(time);
        var owner = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;
        var ids = new List<Guid>();

        for (var i = 0; i < 9; i++)
        {
            var state = await store.GetOrCreateAsync(owner, Guid.NewGuid(), $"fp-{i}",
                () => Task.FromResult(MakeState(owner, Guid.NewGuid(), $"fp-{i}", 1)), ct);
            ids.Add(state.SessionId);
        }

        var evicted = ids.Min();
        store.TryGet(evicted, owner, out _, out _).Should().BeFalse();
        foreach (var id in ids.Where(id => id != evicted))
            store.TryGet(id, owner, out _, out _).Should().BeTrue();
    }

    [Fact]
    public async Task TryGet_expiry_boundary_holds_until_timeout_passes()
    {
        var time = new TestTimeProvider(Start);
        using var store = CreateStore(time);
        var owner = Guid.NewGuid();
        var request = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        var first = await store.GetOrCreateAsync(owner, Guid.NewGuid(), "fp",
            () => Task.FromResult(MakeState(owner, request, "fp", 2)), ct);
        var second = await store.GetOrCreateAsync(owner, Guid.NewGuid(), "fp2",
            () => Task.FromResult(MakeState(owner, request, "fp2", 2)), ct);

        time.Advance(TimeSpan.FromHours(24));
        store.TryGet(first.SessionId, owner, out _, out _).Should().BeTrue();

        time.Advance(TimeSpan.FromTicks(1));
        store.TryGet(second.SessionId, owner, out _, out _).Should().BeFalse();
    }

    [Fact]
    public async Task Remove_during_read_keeps_returned_snapshot_usable()
    {
        var time = new TestTimeProvider(Start);
        using var store = CreateStore(time);
        var owner = Guid.NewGuid();
        var request = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        var created = await store.GetOrCreateAsync(owner, request, "fp",
            () => Task.FromResult(MakeState(owner, request, "fp", 3)), ct);

        store.TryGet(created.SessionId, owner, out var snapshot, out _).Should().BeTrue();
        store.Remove(created.SessionId, owner).Should().BeTrue();
        store.TryGet(created.SessionId, owner, out _, out _).Should().BeFalse();

        snapshot.Should().NotBeNull();
        snapshot!.TotalCount.Should().Be(3);
        snapshot.Order.Should().HaveCount(3);
    }

    [Fact]
    public async Task Replay_refreshes_idle_expiry()
    {
        var time = new TestTimeProvider(Start);
        using var store = CreateStore(time);
        var owner = Guid.NewGuid();
        var request = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        var state = await store.GetOrCreateAsync(owner, request, "fp",
            () => Task.FromResult(MakeState(owner, request, "fp", 2)), ct);

        time.Advance(TimeSpan.FromHours(23));
        var replayed = await store.GetOrCreateAsync(owner, request, "fp",
            () => Task.FromResult(MakeState(owner, request, "fp", 2)), ct);
        replayed.SessionId.Should().Be(state.SessionId);

        time.Advance(TimeSpan.FromHours(2));
        store.TryGet(state.SessionId, owner, out _, out _).Should().BeTrue();
    }

    [Fact]
    public void Remove_missing_or_foreign_returns_false()
    {
        var time = new TestTimeProvider(Start);
        using var store = CreateStore(time);

        store.Remove(Guid.NewGuid(), Guid.NewGuid()).Should().BeFalse();
    }
}