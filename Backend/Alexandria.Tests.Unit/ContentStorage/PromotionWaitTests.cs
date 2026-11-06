using System.Linq.Expressions;
using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Config;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Queues;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Services.Storage.Content;
using Alexandria.Services.Storage.Content.Mime;
using Alexandria.Tests.Unit.Common;
using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using IoDirectory = System.IO.Directory;

namespace Alexandria.Tests.Unit.ContentStorage;

public class PromotionWaitTests
{
    private const string UploadBucket = "files";
    private const string TempBucket = "temp";

    private static readonly TimeSpan ShortPoll = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan LongLimit = TimeSpan.FromSeconds(15);

    // Some tests go through the real five-minute wait, so a broken wait has to fail them rather
    // than stall the whole run.
    private static readonly TimeSpan HangLimit = TimeSpan.FromSeconds(15);

    private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly RecordingLogger<ContentStorageService> _logger = new();
    private readonly Guid _contentObjectId = Guid.NewGuid();
    private readonly Guid _versionId = Guid.NewGuid();

    public PromotionWaitTests()
    {
        var version = new FileVersion
        {
            Id = _versionId,
            ContentObjectId = _contentObjectId,
            ContentHash = [0xAB],
            MimeType = "video/mp4",
            Size = 3,
            VersionNumber = 1
        };

        _unitOfWork.FileVersions.GetByIdAsync(_versionId, Arg.Any<CancellationToken>()).Returns(version);
        _unitOfWork.FileVersions
            .FirstOrDefaultAsync(Arg.Any<Expression<Func<FileVersion, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(version);
        _unitOfWork.ContentObjects.GetByIdAsync(_contentObjectId, Arg.Any<CancellationToken>())
            .Returns(new ContentObject { Id = _contentObjectId, Hash = [0xAB], StorageKey = "content/ab", IsPromoted = false });

        _s3.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => new GetObjectResponse { ResponseStream = new MemoryStream([1, 2, 3]) });
    }

    private ContentStorageService CreateSut()
    {
        var options = Substitute.For<IOptions<S3Config>>();
        options.Value.Returns(new S3Config { UploadBucket = UploadBucket, TempBucket = TempBucket });

        return new ContentStorageService(
            _s3,
            Substitute.For<IAmazonS3>(),
            _unitOfWork,
            options,
            _logger,
            Substitute.For<IPromotionQueue>(),
            Substitute.For<IFileService>(),
            new AuditContext(),
            Substitute.For<IUserSettingsService>(),
            Substitute.For<IPublisherService>(),
            new MimeTypeResolver(Substitute.For<IMimeSniffer>()));
    }

    private void PromotedAfterChecks(int checks)
    {
        var count = 0;
        _unitOfWork.ContentObjects.IsPromotedAsync(_contentObjectId, Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref count) > checks);
    }

    private void NeverPromoted() =>
        _unitOfWork.ContentObjects.IsPromotedAsync(_contentObjectId, Arg.Any<CancellationToken>()).Returns(false);

    private static CancellationTokenSource CancelAfter(TimeSpan delay)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(delay);
        return cts;
    }

    private static CancellationTokenSource CancelSoon() => CancelAfter(TimeSpan.FromMilliseconds(100));

    [Fact]
    public async Task WaitUntilPromotedAsync_alreadyPromoted_returnsAfterOneCheckWithoutLogging()
    {
        PromotedAfterChecks(0);

        await CreateSut().WaitUntilPromotedAsync(
            _contentObjectId, LongLimit, ShortPoll, TestContext.Current.CancellationToken);

        await _unitOfWork.ContentObjects.Received(1)
            .IsPromotedAsync(_contentObjectId, Arg.Any<CancellationToken>());
        _logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task WaitUntilPromotedAsync_promotedWhileWaiting_keepsPollingThenReturns()
    {
        PromotedAfterChecks(3);

        await CreateSut().WaitUntilPromotedAsync(
            _contentObjectId, LongLimit, ShortPoll, TestContext.Current.CancellationToken);

        await _unitOfWork.ContentObjects.Received(4)
            .IsPromotedAsync(_contentObjectId, Arg.Any<CancellationToken>());
        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Information && e.Message.Contains("waiting before"));
        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Information && e.Message.Contains("after waiting"));
    }

    [Fact]
    public async Task WaitUntilPromotedAsync_neverPromoted_throwsAtTheLimitAndLogsAnError()
    {
        NeverPromoted();

        var act = () => CreateSut().WaitUntilPromotedAsync(
            _contentObjectId, TimeSpan.FromMilliseconds(50), ShortPoll, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ContentNotPromotedException>())
            .Which.ContentObjectId.Should().Be(_contentObjectId);
        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task WaitUntilPromotedAsync_cancelled_throwsOperationCanceled()
    {
        NeverPromoted();
        using var cts = CancelSoon();

        var act = () => CreateSut().WaitUntilPromotedAsync(_contentObjectId, LongLimit, ShortPoll, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task DownloadContentObjectAsync_notPromotedAtFirst_waitsThenReadsThePermanentBucket()
    {
        PromotedAfterChecks(1);
        using var cts = CancelAfter(HangLimit);
        var directory = IoDirectory.CreateTempSubdirectory("promotion-wait-test-").FullName;

        try
        {
            await CreateSut().DownloadContentObjectAsync(
                _contentObjectId, Path.Combine(directory, "source"), cts.Token);

            await _s3.Received(1).GetObjectAsync(UploadBucket, "content/ab", Arg.Any<CancellationToken>());
            await _s3.DidNotReceive().GetObjectAsync(TempBucket, Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            IoDirectory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadContentObjectAsync_notPromoted_doesNotTouchStorage()
    {
        NeverPromoted();
        using var cts = CancelSoon();

        var act = () => CreateSut().DownloadContentObjectAsync(_contentObjectId, "/nonexistent/source", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        await _s3.DidNotReceiveWithAnyArgs().GetObjectAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StreamFile_notPromoted_doesNotTouchStorage()
    {
        NeverPromoted();
        using var cts = CancelSoon();

        var act = () => CreateSut().StreamFile(_versionId, new MemoryStream(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        await _s3.DidNotReceiveWithAnyArgs().GetObjectAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StreamFile_promotedAfterAWait_readsThePermanentBucket()
    {
        PromotedAfterChecks(1);
        using var cts = CancelAfter(HangLimit);
        var destination = new MemoryStream();

        await CreateSut().StreamFile(_versionId, destination, cts.Token);

        destination.Length.Should().Be(3);
        await _s3.Received(1).GetObjectAsync(UploadBucket, "content/ab", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DownloadSeekableFile_workerOverloadNotPromoted_doesNotTouchStorage()
    {
        NeverPromoted();
        using var cts = CancelSoon();

        var act = () => CreateSut().DownloadSeekableFile(_versionId, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        await _s3.DidNotReceiveWithAnyArgs()
            .GetObjectMetadataAsync(default(string)!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DownloadSeekableFile_apiOverload_doesNotWaitForPromotion()
    {
        NeverPromoted();
        using var cts = CancelAfter(HangLimit);

        // The fake storage may make the read itself fail. Only the promotion check matters here: a
        // request must never sit in a worker's wait.
        await Record.ExceptionAsync(() =>
            CreateSut().DownloadSeekableFile(_versionId, Guid.NewGuid(), cts.Token));

        await _unitOfWork.ContentObjects.DidNotReceive()
            .IsPromotedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DownloadFile_apiPath_doesNotWaitForPromotion()
    {
        NeverPromoted();
        using var cts = CancelAfter(HangLimit);

        await Record.ExceptionAsync(() => CreateSut().DownloadFile(_versionId, Guid.NewGuid(), cts.Token));

        await _unitOfWork.ContentObjects.DidNotReceive()
            .IsPromotedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
