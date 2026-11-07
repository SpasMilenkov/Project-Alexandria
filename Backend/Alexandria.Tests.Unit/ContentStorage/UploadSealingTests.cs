using System.Linq.Expressions;
using System.Net;
using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Config;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Queues;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Storage.Content;
using Alexandria.Services.Storage.Content.Mime;
using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using Blake3;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Alexandria.Tests.Unit.ContentStorage;

public class UploadSealingTests
{
    private const string TempBucket = "alexandria-temp";

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IUploadRepository _uploads = Substitute.For<IUploadRepository>();
    private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();
    private readonly IPromotionQueue _promotionQueue = Substitute.For<IPromotionQueue>();
    private readonly IFileService _fileService = Substitute.For<IFileService>();
    private readonly ContentStorageService _sut;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly byte[] _bytes = "sealed-bytes"u8.ToArray();
    private readonly Upload _upload;

    public UploadSealingTests()
    {
        _unitOfWork.Uploads.Returns(_uploads);

        _upload = new Upload
        {
            Id = Guid.NewGuid(),
            Status = UploadStatus.Finalizing,
            Hash = Digest(_bytes),
            TempObjectKey = Guid.NewGuid().ToString(),
            Size = _bytes.Length,
            MimeType = "application/octet-stream",
            UserId = _userId
        };

        _uploads.TryClaimForFinalizeAsync(_upload.Id, _userId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _uploads.FirstOrDefaultAsync(Arg.Any<Expression<Func<Upload, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(_upload);

        StoreSealedBytes(_bytes);

        var config = Substitute.For<IOptions<S3Config>>();
        config.Value.Returns(new S3Config());

        _sut = new ContentStorageService(
            _s3,
            Substitute.For<IAmazonS3>(),
            _unitOfWork,
            config,
            Substitute.For<ILogger<ContentStorageService>>(),
            _promotionQueue,
            _fileService,
            new AuditContext(),
            Substitute.For<IUserSettingsService>(),
            Substitute.For<IPublisherService>(),
            new MimeTypeResolver(Substitute.For<IMimeSniffer>()));
    }

    private static byte[] Digest(byte[] input)
    {
        using var hasher = Hasher.New();
        hasher.Update(input);
        return hasher.Finalize().AsSpan().ToArray();
    }

    private void StoreSealedBytes(byte[] bytes)
    {
        _s3.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => new GetObjectResponse
            {
                ResponseStream = new MemoryStream(bytes),
                ContentLength = bytes.Length
            });
    }

    private string IncomingKey => $"{TempObjectKeys.Incoming}{_upload.TempObjectKey}";
    private string SealedKey => $"{TempObjectKeys.Sealed}{_upload.TempObjectKey}";

    [Fact]
    public async Task VerifyUploadAsync_copies_to_the_sealed_key_and_hashes_that_copy()
    {
        var ct = TestContext.Current.CancellationToken;

        var verified = await _sut.VerifyUploadAsync(_upload.Id, _userId, null, ct);

        verified.Hash.Should().Equal(_upload.Hash);
        verified.Size.Should().Be(_bytes.Length);

        Received.InOrder(() =>
        {
            _uploads.TryClaimForFinalizeAsync(_upload.Id, _userId, Arg.Any<DateTime>(), ct);
            _s3.CopyObjectAsync(
                Arg.Is<CopyObjectRequest>(r =>
                    r.SourceBucket == TempBucket
                    && r.SourceKey == IncomingKey
                    && r.DestinationBucket == TempBucket
                    && r.DestinationKey == SealedKey),
                ct);
            _s3.DeleteObjectAsync(TempBucket, IncomingKey, ct);
            _s3.GetObjectAsync(Arg.Is<GetObjectRequest>(r => r.Key == SealedKey), ct);
        });
    }

    [Fact]
    public async Task VerifyUploadAsync_holds_no_transaction_while_reading_the_object()
    {
        var ct = TestContext.Current.CancellationToken;

        await _sut.VerifyUploadAsync(_upload.Id, _userId, null, ct);

        await _unitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyUploadAsync_claim_lost_never_touches_storage()
    {
        var ct = TestContext.Current.CancellationToken;

        _uploads.TryClaimForFinalizeAsync(_upload.Id, _userId, Arg.Any<DateTime>(), ct)
            .Returns(false);

        var act = () => _sut.VerifyUploadAsync(_upload.Id, _userId, null, ct);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _s3.DidNotReceive().CopyObjectAsync(Arg.Any<CopyObjectRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyUploadAsync_unavailable_directory_leaves_the_upload_unclaimed()
    {
        var ct = TestContext.Current.CancellationToken;
        var directoryId = Guid.NewGuid();

        _fileService.FolderWithOwnershipExistsAsync(directoryId, _userId, ct)
            .ThrowsAsync(new DirectoryNotFoundException());

        var act = () => _sut.VerifyUploadAsync(_upload.Id, _userId, directoryId, ct);

        await act.Should().ThrowAsync<DirectoryNotFoundException>();
        await _uploads.DidNotReceive().TryClaimForFinalizeAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyUploadAsync_hash_mismatch_interrupts_the_upload_and_removes_both_objects()
    {
        var ct = TestContext.Current.CancellationToken;

        StoreSealedBytes("swapped-bytes"u8.ToArray());
        _upload.Size = 1024;

        var act = () => _sut.VerifyUploadAsync(_upload.Id, _userId, null, ct);

        await act.Should().ThrowAsync<UploadVerificationException>();

        await _uploads.Received(1).MarkInterruptedAsync(_upload.Id, Arg.Any<DateTime>(), CancellationToken.None);
        await _s3.Received(1).DeleteObjectAsync(TempBucket, SealedKey, CancellationToken.None);
        await _s3.Received().DeleteObjectAsync(TempBucket, IncomingKey, Arg.Any<CancellationToken>());
        await _promotionQueue.DidNotReceive().QueuePromotionAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyUploadAsync_object_larger_than_declared_fails_verification()
    {
        var ct = TestContext.Current.CancellationToken;

        _upload.Size = _bytes.Length - 1;

        var act = () => _sut.VerifyUploadAsync(_upload.Id, _userId, null, ct);

        await act.Should().ThrowAsync<UploadVerificationException>();
        await _uploads.Received(1).MarkInterruptedAsync(_upload.Id, Arg.Any<DateTime>(), CancellationToken.None);
    }

    [Fact]
    public async Task VerifyUploadAsync_nothing_uploaded_fails_verification()
    {
        var ct = TestContext.Current.CancellationToken;

        _s3.CopyObjectAsync(Arg.Any<CopyObjectRequest>(), ct)
            .ThrowsAsync(new AmazonS3Exception("NoSuchKey") { StatusCode = HttpStatusCode.NotFound });

        var act = () => _sut.VerifyUploadAsync(_upload.Id, _userId, null, ct);

        await act.Should().ThrowAsync<UploadVerificationException>();
        await _uploads.Received(1).MarkInterruptedAsync(_upload.Id, Arg.Any<DateTime>(), CancellationToken.None);
    }

    [Fact]
    public async Task VerifyUploadAsync_request_cancelled_still_ends_the_claim()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _s3.CopyObjectAsync(Arg.Any<CopyObjectRequest>(), ct)
            .Returns<CopyObjectResponse>(_ =>
            {
                cts.Cancel();
                throw new OperationCanceledException(ct);
            });

        var act = () => _sut.VerifyUploadAsync(_upload.Id, _userId, null, ct);

        await act.Should().ThrowAsync<OperationCanceledException>();
        await _uploads.Received(1).MarkInterruptedAsync(_upload.Id, Arg.Any<DateTime>(), CancellationToken.None);
    }

    [Fact]
    public async Task FinalizeFileUpload_verification_failure_is_wrapped_with_its_cause()
    {
        var ct = TestContext.Current.CancellationToken;

        StoreSealedBytes("swapped-bytes"u8.ToArray());
        _upload.Size = 1024;

        var act = () => _sut.FinalizeFileUpload(
            "file.bin", _upload.Id, _userId, null, null, null, null, null, ct: ct);

        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.InnerException.Should().BeOfType<UploadVerificationException>();
    }
}
