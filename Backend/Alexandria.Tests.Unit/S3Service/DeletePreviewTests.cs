using System.Linq.Expressions;
using System.Net;
using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Config;
using Alexandria.Common.Queues;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Tests.Common.Builders;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Tests.Unit.S3Service;

public class DeletePreviewTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileRepository _files = Substitute.For<IFileRepository>();
    private readonly IPreviewRepository _previews = Substitute.For<IPreviewRepository>();
    private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();
    private readonly Services.Storage.S3Service _sut;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _previewId = Guid.NewGuid();
    private readonly Guid _fileId = Guid.NewGuid();

    public DeletePreviewTests()
    {
        _unitOfWork.Files.Returns(_files);
        _unitOfWork.Previews.Returns(_previews);

        var config = Substitute.For<IOptions<S3Config>>();
        config.Value.Returns(new S3Config());

        _s3.DeleteObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteObjectResponse());

        _sut = new Services.Storage.S3Service(
            _s3,
            Substitute.For<IAmazonS3>(),
            _unitOfWork,
            config,
            Substitute.For<ILogger<Services.Storage.S3Service>>(),
            Substitute.For<IPromotionQueue>(),
            Substitute.For<IFileService>(),
            new AuditContext(),
            Substitute.For<IUserSettingsService>());
    }

    private File OwnedFile(Guid? fileId = null, Guid? ownerId = null)
        => new FileBuilder()
            .WithId(fileId ?? _fileId)
            .WithOwner(ownerId ?? _userId)
            .Build();

    private static FileVersion VersionFor(File file, byte[]? hash = null)
        => new()
        {
            ContentHash = hash ?? new byte[] { 0xAB, 0x12 },
            Size = 2048L,
            VersionNumber = 1,
            MimeType = "image/png",
            CreatedBy = file.OwnerId,
            ContentObjectId = Guid.NewGuid(),
            FileId = file.Id,
            File = file
        };

    private Preview StoredPreview(File file, string objectKey = "previews/ab12", long size = 1234L)
    {
        var version = VersionFor(file);
        return new Preview
        {
            Id = _previewId,
            MimeType = "image/png",
            Size = size,
            Kind = PreviewKind.Preview,
            VersionId = version.Id,
            Version = version,
            ObjectKey = objectKey
        };
    }

    private void NoSharing()
    {
        _previews.ExistsAsync(
                Arg.Any<Expression<Func<Preview, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(false);
    }

    [Fact]
    public async Task delete_single_owner_deletes_object_row_returns_size()
    {
        var ct = TestContext.Current.CancellationToken;
        _previews.GetWithFileAsync(_previewId, ct).Returns(StoredPreview(OwnedFile()));
        NoSharing();

        var freed = await _sut.DeletePreviewAsync(_previewId, _userId, false, ct);

        freed.Should().Be(1234L);
        await _s3.Received(1).DeleteObjectAsync("user-previews", "previews/ab12", ct);
        _previews.Received(1).Remove(Arg.Is<Preview>(p => p.Id == _previewId));
    }

    [Fact]
    public async Task delete_single_admin_bypasses_ownership()
    {
        var ct = TestContext.Current.CancellationToken;
        _previews.GetWithFileAsync(_previewId, ct)
            .Returns(StoredPreview(OwnedFile(ownerId: Guid.NewGuid())));
        NoSharing();

        var freed = await _sut.DeletePreviewAsync(_previewId, _userId, true, ct);

        freed.Should().Be(1234L);
        await _s3.Received(1).DeleteObjectAsync(Arg.Any<string>(), Arg.Any<string>(), ct);
    }

    [Fact]
    public async Task delete_single_missing_throws_not_found()
    {
        var ct = TestContext.Current.CancellationToken;
        _previews.GetWithFileAsync(_previewId, ct).Returns((Preview?)null);

        var act = () => _sut.DeletePreviewAsync(_previewId, _userId, false, ct);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        await _s3.DidNotReceive().DeleteObjectAsync(Arg.Any<string>(), Arg.Any<string>(), ct);
    }

    [Fact]
    public async Task delete_single_without_file_throws_not_found()
    {
        var ct = TestContext.Current.CancellationToken;
        var preview = StoredPreview(OwnedFile());
        preview.Version = null;
        _previews.GetWithFileAsync(_previewId, ct).Returns(preview);

        var act = () => _sut.DeletePreviewAsync(_previewId, _userId, false, ct);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        await _s3.DidNotReceive().DeleteObjectAsync(Arg.Any<string>(), Arg.Any<string>(), ct);
    }

    [Fact]
    public async Task delete_single_foreign_non_admin_throws_forbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        _previews.GetWithFileAsync(_previewId, ct)
            .Returns(StoredPreview(OwnedFile(ownerId: Guid.NewGuid())));

        var act = () => _sut.DeletePreviewAsync(_previewId, _userId, false, ct);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        await _s3.DidNotReceive().DeleteObjectAsync(Arg.Any<string>(), Arg.Any<string>(), ct);
        _previews.DidNotReceive().Remove(Arg.Any<Preview>());
    }

    [Fact]
    public async Task delete_single_shared_key_keeps_object_removes_row()
    {
        var ct = TestContext.Current.CancellationToken;
        _previews.GetWithFileAsync(_previewId, ct).Returns(StoredPreview(OwnedFile()));
        _previews.ExistsAsync(
                Arg.Any<Expression<Func<Preview, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var freed = await _sut.DeletePreviewAsync(_previewId, _userId, false, ct);

        freed.Should().Be(1234L);
        await _s3.DidNotReceive().DeleteObjectAsync(Arg.Any<string>(), Arg.Any<string>(), ct);
        _previews.Received(1).Remove(Arg.Is<Preview>(p => p.Id == _previewId));
    }

    [Fact]
    public async Task delete_single_missing_object_still_removes_row()
    {
        var ct = TestContext.Current.CancellationToken;
        _previews.GetWithFileAsync(_previewId, ct).Returns(StoredPreview(OwnedFile()));
        NoSharing();
        _s3.DeleteObjectAsync(Arg.Any<string>(), Arg.Any<string>(), ct)
            .Returns<DeleteObjectResponse>(_ => throw new AmazonS3Exception(
                "not found", ErrorType.Sender, "NoSuchKey", "req", HttpStatusCode.NotFound));

        var freed = await _sut.DeletePreviewAsync(_previewId, _userId, false, ct);

        freed.Should().Be(1234L);
        _previews.Received(1).Remove(Arg.Is<Preview>(p => p.Id == _previewId));
    }

    [Fact]
    public void resolve_key_prefers_stored_object_key()
    {
        var preview = StoredPreview(OwnedFile(), objectKey: "previews/custom");

        Services.Storage.S3Service.ResolvePreviewKey(preview).Should().Be("previews/custom");
    }

    [Fact]
    public void resolve_key_falls_back_to_hash_convention_per_kind()
    {
        var file = OwnedFile();
        var version = VersionFor(file, new byte[] { 0x0A, 0xFF });
        var thumbnail = new Preview
        {
            Id = Guid.NewGuid(),
            MimeType = "image/png",
            Size = 10L,
            Kind = PreviewKind.Thumbnail,
            VersionId = version.Id,
            Version = version,
            ObjectKey = string.Empty
        };
        var image = new Preview
        {
            Id = Guid.NewGuid(),
            MimeType = "image/png",
            Size = 10L,
            Kind = PreviewKind.Preview,
            VersionId = version.Id,
            Version = version,
            ObjectKey = string.Empty
        };

        Services.Storage.S3Service.ResolvePreviewKey(thumbnail).Should().Be("thumbnails/0aff");
        Services.Storage.S3Service.ResolvePreviewKey(image).Should().Be("previews/0aff");
    }

    [Fact]
    public async Task delete_by_file_returns_counts_and_deletes_each()
    {
        var ct = TestContext.Current.CancellationToken;
        var file = OwnedFile();
        _files.GetByIdAsync(_fileId, ct).Returns(file);

        var first = StoredPreview(file, "previews/aa", 100L);
        var second = new Preview
        {
            Id = Guid.NewGuid(),
            MimeType = "image/png",
            Size = 200L,
            Kind = PreviewKind.Thumbnail,
            VersionId = first.VersionId,
            Version = first.Version,
            ObjectKey = "thumbnails/aa"
        };
        _previews.GetByFileAsync(_fileId, null, ct)
            .Returns(new List<Preview> { first, second });
        NoSharing();

        var (deletedCount, freedBytes) =
            await _sut.DeletePreviewsByFileAsync(_fileId, _userId, false, null, ct);

        deletedCount.Should().Be(2);
        freedBytes.Should().Be(300L);
        await _s3.Received(1).DeleteObjectAsync("user-previews", "previews/aa", ct);
        await _s3.Received(1).DeleteObjectAsync("user-previews", "thumbnails/aa", ct);
        _previews.Received(2).Remove(Arg.Any<Preview>());
    }

    [Fact]
    public async Task delete_by_file_missing_file_throws_not_found()
    {
        var ct = TestContext.Current.CancellationToken;
        _files.GetByIdAsync(_fileId, ct).Returns((File?)null);

        var act = () => _sut.DeletePreviewsByFileAsync(_fileId, _userId, false, null, ct);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task delete_by_file_foreign_non_admin_throws_forbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        _files.GetByIdAsync(_fileId, ct).Returns(OwnedFile(ownerId: Guid.NewGuid()));

        var act = () => _sut.DeletePreviewsByFileAsync(_fileId, _userId, false, null, ct);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        await _s3.DidNotReceive().DeleteObjectAsync(Arg.Any<string>(), Arg.Any<string>(), ct);
    }

    [Fact]
    public async Task delete_by_file_empty_returns_zero()
    {
        var ct = TestContext.Current.CancellationToken;
        _files.GetByIdAsync(_fileId, ct).Returns(OwnedFile());
        _previews.GetByFileAsync(_fileId, null, ct).Returns(new List<Preview>());

        var (deletedCount, freedBytes) =
            await _sut.DeletePreviewsByFileAsync(_fileId, _userId, false, null, ct);

        deletedCount.Should().Be(0);
        freedBytes.Should().Be(0L);
        await _s3.DidNotReceive().DeleteObjectAsync(Arg.Any<string>(), Arg.Any<string>(), ct);
    }
}