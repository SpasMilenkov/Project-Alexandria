using Alexandria.Common;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using Alexandria.Services.Storage.Content;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Alexandria.Tests.Unit.ContentStorage;

public class UploadBatchServiceTests
{
    private const string InternalDetail = "bucket alexandria-temp refused the key";

    private readonly IContentStorageService _storage = Substitute.For<IContentStorageService>();
    private readonly IPolicyDispatcher _dispatcher = Substitute.For<IPolicyDispatcher>();
    private readonly IUploadRepository _uploads = Substitute.For<IUploadRepository>();
    private readonly Guid _userId = Guid.NewGuid();
    private int _storageResolutions;

    private UploadBatchService CreateSut()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Uploads.Returns(_uploads);

        _uploads.GetFinishedIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid>());

        _storage.VerifyUploadAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(ci => Verified(ci.ArgAt<Guid>(0)));

        _storage.CommitUploadAsync(
                Arg.Any<VerifiedUpload>(), Arg.Any<UploadFinalizeItem>(), Arg.Any<CancellationToken>())
            .Returns(ci => Committed(ci.Arg<UploadFinalizeItem>()));

        var services = new ServiceCollection();

        services.AddScoped(_ => unitOfWork);
        services.AddScoped(_ => _dispatcher);
        services.AddScoped(_ =>
        {
            Interlocked.Increment(ref _storageResolutions);
            return _storage;
        });

        return new UploadBatchService(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Substitute.For<ILogger<UploadBatchService>>());
    }

    // Hash derived from the ID, so items only count as identical when a test says they are.
    private VerifiedUpload Verified(Guid uploadId, byte[]? hash = null) => new()
    {
        UploadId = uploadId,
        UploadedBy = _userId,
        TempObjectKey = uploadId.ToString(),
        Hash = hash ?? uploadId.ToByteArray(),
        Size = 5,
        Header = [1, 2, 3]
    };

    private static UploadResult Committed(UploadFinalizeItem item) =>
        new(item.FileName, 5, Guid.NewGuid(), "text/plain", Guid.NewGuid(), false, item.IsEncrypted);

    private static UploadFinalizeItem Item(string fileName = "file.txt", bool isEncrypted = false) => new()
    {
        UploadId = Guid.NewGuid(),
        DirectoryId = null,
        FileName = fileName,
        IsEncrypted = isEncrypted,
        EncryptionIv = null,
        EncryptionSalt = null,
        IntegrityTag = null,
        EncryptionHint = null,
        IterationCount = null
    };

    [Fact]
    public async Task FinalizeAsync_middle_item_fails_verification_neighbours_still_commit()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();
        var items = new[] { Item("a"), Item("b"), Item("c") };

        _storage.VerifyUploadAsync(items[1].UploadId, _userId, null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new UploadVerificationException("Hash mismatch"));

        var results = await sut.FinalizeAsync(items, _userId, ct);

        results.Select(r => r.UploadId).Should().Equal(items.Select(i => i.UploadId));
        results.Select(r => r.Success).Should().Equal(true, false, true);
        results[1].Error.Should().Be(UploadErrorMessages.FailedVerification);

        await _storage.Received(2).CommitUploadAsync(
            Arg.Any<VerifiedUpload>(), Arg.Any<UploadFinalizeItem>(), Arg.Any<CancellationToken>());
        await _storage.DidNotReceive().CommitUploadAsync(
            Arg.Any<VerifiedUpload>(), items[1], Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FinalizeAsync_commit_fails_once_retries_in_a_fresh_scope()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();
        var item = Item();
        var calls = 0;
        var resolutionsPerAttempt = new List<int>();

        _storage.CommitUploadAsync(Arg.Any<VerifiedUpload>(), item, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                resolutionsPerAttempt.Add(Volatile.Read(ref _storageResolutions));

                if (++calls == 1)
                    throw new InvalidOperationException("duplicate key value");

                return Committed(item);
            });

        var results = await sut.FinalizeAsync([item], _userId, ct);

        results[0].Success.Should().BeTrue();
        calls.Should().Be(2);
        resolutionsPerAttempt[1].Should().BeGreaterThan(resolutionsPerAttempt[0]);

        await _storage.DidNotReceive().AbandonUploadAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FinalizeAsync_commit_keeps_failing_abandons_and_hides_internal_detail()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();
        var item = Item();

        _storage.CommitUploadAsync(Arg.Any<VerifiedUpload>(), item, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException(InternalDetail));

        var results = await sut.FinalizeAsync([item], _userId, ct);

        results[0].Success.Should().BeFalse();
        results[0].Error.Should().Be(UploadErrorMessages.Generic);
        results[0].Error.Should().NotContain("alexandria-temp");

        await _storage.Received(2).CommitUploadAsync(
            Arg.Any<VerifiedUpload>(), item, Arg.Any<CancellationToken>());
        await _storage.Received(1).AbandonUploadAsync(
            item.UploadId, item.UploadId.ToString(), CancellationToken.None);
    }

    [Fact]
    public async Task FinalizeAsync_missing_directory_reports_destination_not_found()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();
        var item = Item();

        _storage.VerifyUploadAsync(item.UploadId, _userId, null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new DirectoryNotFoundException());

        var results = await sut.FinalizeAsync([item], _userId, ct);

        results[0].Error.Should().Be(UploadErrorMessages.DestinationNotFound);
    }

    [Fact]
    public async Task FinalizeAsync_dispatch_fails_still_reports_the_saved_file_as_success()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();

        _dispatcher.DispatchAsync(Arg.Any<FileFinalizedEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("broker down"));

        var results = await sut.FinalizeAsync([Item()], _userId, ct);

        results[0].Success.Should().BeTrue();
        await _storage.Received(1).CommitUploadAsync(
            Arg.Any<VerifiedUpload>(), Arg.Any<UploadFinalizeItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FinalizeAsync_encrypted_item_skips_policy_dispatch()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();

        await sut.FinalizeAsync([Item("plain.txt"), Item("secret.bin", isEncrypted: true)], _userId, ct);

        await _dispatcher.Received(1).DispatchAsync(
            Arg.Is<FileFinalizedEvent>(e => e.FileName == "plain.txt"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FinalizeAsync_already_finished_upload_succeeds_without_verifying_again()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();
        var item = Item();

        _uploads.GetFinishedIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), _userId, Arg.Any<CancellationToken>())
            .Returns([item.UploadId]);

        var results = await sut.FinalizeAsync([item], _userId, ct);

        results[0].Success.Should().BeTrue();
        await _storage.DidNotReceive().VerifyUploadAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FinalizeAsync_client_disconnects_stops_and_abandons_uncommitted_uploads()
    {
        using var disconnect = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // One destination, so they commit in order and the cancel lands before the second.
        var items = new[] { Item("same"), Item("same"), Item("same") };

        _storage.CommitUploadAsync(Arg.Any<VerifiedUpload>(), items[0], Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                disconnect.Cancel();
                return Committed(items[0]);
            });

        var act = () => sut.FinalizeAsync(items, _userId, disconnect.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();

        await _storage.DidNotReceive().AbandonUploadAsync(
            items[0].UploadId, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _storage.Received(1).AbandonUploadAsync(
            items[1].UploadId, items[1].UploadId.ToString(), CancellationToken.None);
        await _storage.Received(1).AbandonUploadAsync(
            items[2].UploadId, items[2].UploadId.ToString(), CancellationToken.None);
    }

    private void TrackConcurrency(Func<Guid, VerifiedUpload> verifiedFor, ConcurrencyPeaks peaks)
    {
        _storage.VerifyUploadAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                InterlockedMax(ref peaks.Verifies, Interlocked.Increment(ref peaks.ActiveVerifies));

                await Task.Delay(30, CancellationToken.None);

                Interlocked.Decrement(ref peaks.ActiveVerifies);
                return verifiedFor(ci.ArgAt<Guid>(0));
            });

        _storage.CommitUploadAsync(
                Arg.Any<VerifiedUpload>(), Arg.Any<UploadFinalizeItem>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                InterlockedMax(ref peaks.Commits, Interlocked.Increment(ref peaks.ActiveCommits));

                await Task.Delay(20, CancellationToken.None);

                Interlocked.Decrement(ref peaks.ActiveCommits);
                return Committed(ci.Arg<UploadFinalizeItem>());
            });
    }

    [Fact]
    public async Task FinalizeAsync_unrelated_files_verify_and_commit_in_parallel_within_the_limit()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();
        var peaks = new ConcurrencyPeaks();
        var items = Enumerable.Range(0, 12).Select(i => Item($"file-{i}")).ToArray();

        TrackConcurrency(id => Verified(id), peaks);

        var results = await sut.FinalizeAsync(items, _userId, ct);

        results.Should().OnlyContain(r => r.Success);
        peaks.Verifies.Should().BeInRange(2, 4);
        peaks.Commits.Should().BeInRange(2, 4);
    }

    [Fact]
    public async Task FinalizeAsync_identical_content_commits_one_at_a_time()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();
        var peaks = new ConcurrencyPeaks();
        var sharedHash = new byte[32];
        var items = Enumerable.Range(0, 6).Select(i => Item($"copy-{i}")).ToArray();

        TrackConcurrency(id => Verified(id, sharedHash), peaks);

        var results = await sut.FinalizeAsync(items, _userId, ct);

        results.Should().OnlyContain(r => r.Success);
        peaks.Verifies.Should().BeInRange(2, 4);
        peaks.Commits.Should().Be(1);
    }

    [Fact]
    public async Task FinalizeAsync_same_destination_commits_one_at_a_time()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();
        var peaks = new ConcurrencyPeaks();
        var items = Enumerable.Range(0, 6).Select(_ => Item("same-name.txt")).ToArray();

        TrackConcurrency(id => Verified(id), peaks);

        var results = await sut.FinalizeAsync(items, _userId, ct);

        results.Should().OnlyContain(r => r.Success);
        peaks.Commits.Should().Be(1);
    }

    [Fact]
    public async Task FinalizeAsync_same_name_in_different_directories_does_not_conflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();
        var peaks = new ConcurrencyPeaks();

        var items = Enumerable.Range(0, 8)
            .Select(_ => Item("cover.jpg") with { DirectoryId = Guid.NewGuid() })
            .ToArray();

        TrackConcurrency(id => Verified(id), peaks);

        await sut.FinalizeAsync(items, _userId, ct);

        peaks.Commits.Should().BeInRange(2, 4);
    }

    [Fact]
    public async Task FinalizeAsync_results_stay_in_item_order_when_commits_finish_out_of_order()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();
        var items = Enumerable.Range(0, 8).Select(i => Item($"file-{i}")).ToArray();
        var delays = new Queue<int>([40, 5, 30, 1, 20, 10, 15, 2]);

        _storage.CommitUploadAsync(
                Arg.Any<VerifiedUpload>(), Arg.Any<UploadFinalizeItem>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                int delay;

                lock (delays)
                    delay = delays.Dequeue();

                await Task.Delay(delay, CancellationToken.None);
                return Committed(ci.Arg<UploadFinalizeItem>());
            });

        var results = await sut.FinalizeAsync(items, _userId, ct);

        results.Select(r => r.UploadId).Should().Equal(items.Select(i => i.UploadId));
    }

    private sealed class ConcurrencyPeaks
    {
        public int ActiveVerifies;
        public int ActiveCommits;
        public int Verifies;
        public int Commits;
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;

        do
        {
            current = Volatile.Read(ref target);

            if (value <= current)
                return;
        } while (Interlocked.CompareExchange(ref target, value, current) != current);
    }
}
