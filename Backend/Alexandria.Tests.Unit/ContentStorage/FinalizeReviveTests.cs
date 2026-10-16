using System.Linq.Expressions;
using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Config;
using Alexandria.Common.Queues;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Storage.Cleanup;
using Alexandria.Services.Storage.Content;
using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using Blake3;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Tests.Unit.ContentStorage;

public class FinalizeReviveTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileRepository _files = Substitute.For<IFileRepository>();
    private readonly IFileVersionRepository _versions = Substitute.For<IFileVersionRepository>();
    private readonly IContentObjectRepository _contentObjects = Substitute.For<IContentObjectRepository>();
    private readonly IUploadRepository _uploads = Substitute.For<IUploadRepository>();
    private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();
    private readonly IPromotionQueue _promotionQueue = Substitute.For<IPromotionQueue>();
    private readonly IFileService _fileService = Substitute.For<IFileService>();
    private readonly ContentStorageService _sut;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly byte[] _bytes = "revive-me"u8.ToArray();

    public FinalizeReviveTests()
    {
        _unitOfWork.Files.Returns(_files);
        _unitOfWork.FileVersions.Returns(_versions);
        _unitOfWork.ContentObjects.Returns(_contentObjects);
        _unitOfWork.Uploads.Returns(_uploads);

        _unitOfWork.Previews.Returns(Substitute.For<IPreviewRepository>());
        _unitOfWork.StreamingRepresentations.Returns(Substitute.For<IStreamingRepresentationRepository>());
        _unitOfWork.Users.Returns(Substitute.For<IUserRepository>());

        var config = Substitute.For<IOptions<S3Config>>();
        config.Value.Returns(new S3Config());

        _fileService.FolderWithOwnershipExistsAsync(
                Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

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
            Substitute.For<IPublisherService>());
    }

    private static byte[] Digest(byte[] input)
    {
        using var hasher = Hasher.New();
        hasher.Update(input);
        return hasher.Finalize().AsSpan().ToArray();
    }

    private Upload SeedUpload(out byte[] hash)
    {
        hash = Digest(_bytes);

        var upload = new Upload
        {
            Id = Guid.NewGuid(),
            Status = UploadStatus.InProgress,
            Hash = hash,
            TempObjectKey = Guid.NewGuid().ToString(),
            Size = _bytes.Length,
            MimeType = "application/octet-stream",
            UserId = _userId
        };

        _uploads.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<Upload, bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(upload);

        _s3.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetObjectResponse
            {
                ResponseStream = new MemoryStream(_bytes),
                ContentLength = _bytes.Length
            });

        _files.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<File, bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns((File?)null);

        _files.CreateAsync(Arg.Any<File>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var file = ci.Arg<File>();
                file.Id = Guid.NewGuid();
                return file;
            });

        _versions.AddAsync(Arg.Any<FileVersion>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var version = ci.Arg<FileVersion>();
                version.Id = Guid.NewGuid();
                return version;
            });

        return upload;
    }

    [Fact]
    public async Task revive_promoted_skips_queue_and_cleans_redundant_temp()
    {
        var ct = TestContext.Current.CancellationToken;
        var upload = SeedUpload(out var hash);

        var contentObject = new ContentObject
        {
            Id = Guid.NewGuid(),
            Hash = hash,
            StorageKey = $"content/{Convert.ToHexStringLower(hash)}",
            IsPromoted = true,
            PromotedAt = DateTime.UtcNow.AddDays(-1),
            OrphanedAt = DateTime.UtcNow.AddDays(-1),
            UploadId = Guid.NewGuid(),
            PromotionAttempts = 2
        };
        _contentObjects.HashExistsAsync(Arg.Is<byte[]>(h => h.SequenceEqual(hash)), Arg.Any<CancellationToken>())
            .Returns(contentObject);

        await _sut.FinalizeFileUpload("revived.bin", upload.Id, _userId, null, null, null, null, null, ct: ct);

        // No second row: the orphan was revived in place.
        await _contentObjects.DidNotReceive().AddAsync(
            Arg.Any<ContentObject>(), Arg.Any<CancellationToken>());

        contentObject.OrphanedAt.Should().BeNull();
        contentObject.UploadId.Should().Be(upload.Id);

        // Bytes already durable: no copy queued, redundant temp removed.
        await _promotionQueue.DidNotReceive().QueuePromotionAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _s3.Received(1).DeleteObjectAsync(
            "alexandria-temp", $"content/{upload.TempObjectKey}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task revive_unpromoted_resets_attempts_and_queues_with_new_temp_key()
    {
        var ct = TestContext.Current.CancellationToken;
        var upload = SeedUpload(out var hash);

        var contentObject = new ContentObject
        {
            Id = Guid.NewGuid(),
            Hash = hash,
            StorageKey = $"content/{Convert.ToHexStringLower(hash)}",
            IsPromoted = false,
            OrphanedAt = DateTime.UtcNow.AddDays(-31),
            UploadId = Guid.NewGuid(),
            PromotionAttempts = 10
        };
        _contentObjects.HashExistsAsync(Arg.Is<byte[]>(h => h.SequenceEqual(hash)), Arg.Any<CancellationToken>())
            .Returns(contentObject);

        await _sut.FinalizeFileUpload("revived.bin", upload.Id, _userId, null, null, null, null, null, ct: ct);

        // Attempts reset so the scanner picks the row back up.
        contentObject.PromotionAttempts.Should().Be(0);
        contentObject.OrphanedAt.Should().BeNull();
        contentObject.UploadId.Should().Be(upload.Id);

        // Promotion must read the live temp copy, not the stale birth upload.
        await _promotionQueue.Received(1).QueuePromotionAsync(
            contentObject.Id, upload.TempObjectKey, Arg.Any<CancellationToken>());
        await _s3.DidNotReceive().DeleteObjectAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task orphan_delete_skipped_when_claim_fails()
    {
        var ct = TestContext.Current.CancellationToken;
        var config = Substitute.For<IOptions<S3Config>>();
        config.Value.Returns(new S3Config());

        var worker = new OrphanedCleanupWorker(
            Substitute.For<IServiceProvider>(),
            _s3,
            config,
            Substitute.For<ILogger<OrphanedCleanupWorker>>());

        var contentObject = new ContentObject
        {
            Id = Guid.NewGuid(),
            Hash = Digest(_bytes),
            StorageKey = "content/abc",
            IsPromoted = true,
            OrphanedAt = DateTime.UtcNow.AddDays(-31)
        };

        _contentObjects
            .TryClaimOrphanedDeleteAsync(contentObject.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var deleted = await worker.DeleteOrphanedContentObjectAsync(contentObject, _unitOfWork, ct);

        deleted.Should().BeFalse();
        await _s3.DidNotReceive().DeleteObjectAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task orphan_delete_claimed_removes_object_without_second_delete()
    {
        var ct = TestContext.Current.CancellationToken;
        var config = Substitute.For<IOptions<S3Config>>();
        config.Value.Returns(new S3Config());

        var worker = new OrphanedCleanupWorker(
            Substitute.For<IServiceProvider>(),
            _s3,
            config,
            Substitute.For<ILogger<OrphanedCleanupWorker>>());

        var hash = Digest(_bytes);
        var contentObject = new ContentObject
        {
            Id = Guid.NewGuid(),
            Hash = hash,
            StorageKey = $"content/{Convert.ToHexStringLower(hash)}",
            IsPromoted = true,
            OrphanedAt = DateTime.UtcNow.AddDays(-31)
        };

        _contentObjects
            .TryClaimOrphanedDeleteAsync(contentObject.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var deleted = await worker.DeleteOrphanedContentObjectAsync(contentObject, _unitOfWork, ct);

        deleted.Should().BeTrue();

        // The claim already removed the row; the worker must not delete twice.
        await _contentObjects.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _s3.Received(1).DeleteObjectAsync(
            "user-uploads", $"content/{Convert.ToHexStringLower(hash)}", Arg.Any<CancellationToken>());
    }
}