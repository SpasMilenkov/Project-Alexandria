using System.Linq.Expressions;
using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Config;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Services.Storage.Previews;
using Alexandria.Tests.Common.Builders;
using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Alexandria.Tests.Unit.PreviewArtifacts;

public class CustomCoverTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0x00];

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileVersionRepository _versions = Substitute.For<IFileVersionRepository>();
    private readonly IFileRepository _files = Substitute.For<IFileRepository>();
    private readonly IMediaMetadataRepository _metadata = Substitute.For<IMediaMetadataRepository>();
    private readonly IPreviewRepository _previews = Substitute.For<IPreviewRepository>();
    private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();
    private readonly List<Preview> _rows = [];
    private readonly S3Config _config = new();
    private readonly PreviewArtifactService _sut;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _fileId = Guid.NewGuid();
    private readonly Guid _versionId = Guid.NewGuid();

    public CustomCoverTests()
    {
        _unitOfWork.FileVersions.Returns(_versions);
        _unitOfWork.Files.Returns(_files);
        _unitOfWork.MediaMetadata.Returns(_metadata);
        _unitOfWork.Previews.Returns(_previews);

        // Evaluates the real predicate so a lookup that forgets IsManual or Kind fails its test.
        _previews
            .FirstOrDefaultAsync(Arg.Any<Expression<Func<Preview, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var predicate = call.Arg<Expression<Func<Preview, bool>>>().Compile();
                return Task.FromResult(_rows.FirstOrDefault(predicate));
            });

        _s3.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse());
        _s3.DeleteObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteObjectResponse());

        var options = Substitute.For<IOptions<S3Config>>();
        options.Value.Returns(_config);

        _sut = new PreviewArtifactService(
            _s3,
            _unitOfWork,
            options,
            Substitute.For<ILogger<PreviewArtifactService>>(),
            new AuditContext(),
            Substitute.For<IUserSettingsService>(),
            Substitute.For<IPublisherService>());

        GivenVersion();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private FileVersion GivenVersion(
        string mimeType = "audio/mpeg", bool isEncrypted = false, bool isPromoted = true, Guid? ownerId = null)
    {
        var file = new FileBuilder()
            .WithId(_fileId)
            .WithOwner(ownerId ?? _userId)
            .WithMimeType(mimeType)
            .Build();

        var version = new FileVersionBuilder()
            .WithId(_versionId)
            .WithFileId(_fileId)
            .WithContentObject(new ContentObjectBuilder().WithIsPromoted(isPromoted).Build())
            .Build();

        version.Id = _versionId;
        version.File = file;
        version.IsEncrypted = isEncrypted;

        _versions.GetByIdAsync(_versionId, Arg.Any<CancellationToken>()).Returns(version);

        return version;
    }

    private Preview GivenThumbnail(bool isManual, string objectKey)
    {
        var row = new Preview
        {
            Id = Guid.NewGuid(),
            MimeType = "image/jpeg",
            Size = 10,
            Kind = PreviewKind.Thumbnail,
            VersionId = _versionId,
            ObjectKey = objectKey,
            IsManual = isManual,
            UpdatedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
        };

        _rows.Add(row);

        return row;
    }

    private Task<DateTime> SetCoverAsync(byte[]? image = null) =>
        _sut.SetCustomCoverAsync(_fileId, _versionId, _userId, new MemoryStream(image ?? Jpeg), Ct);

    private Task UploadGeneratedAsync()
    {
        var preview = new MemoryStream([1, 2, 3]);
        var thumbnail = new MemoryStream([4, 5, 6]);

        return _sut.UploadMediaData(
            preview, thumbnail, 3, 3, "hash", _versionId, new MediaMetadataDto { Duration = 180 }, Ct);
    }

    private async Task ExpectRejectedWithoutStoreAsync<TException>(Func<Task> act) where TException : Exception
    {
        await act.Should().ThrowAsync<TException>();

        await _s3.DidNotReceive().PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetCustomCoverAsync_noThumbnailYet_storesVersionScopedManualRow()
    {
        Preview? created = null;
        await _previews.CreateAsync(Arg.Do<Preview>(p => created = p), Arg.Any<CancellationToken>());

        var stamp = await SetCoverAsync();

        created.Should().NotBeNull();
        created.IsManual.Should().BeTrue();
        created.Kind.Should().Be(PreviewKind.Thumbnail);
        created.VersionId.Should().Be(_versionId);
        created.MimeType.Should().Be("image/jpeg");
        created.ObjectKey.Should().StartWith($"track-covers/{_versionId}/");
        created.UpdatedAt.Should().Be(stamp);

        await _s3.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r => r.Key == created.ObjectKey && r.ContentType == "image/jpeg"),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetCustomCoverAsync_automaticThumbnail_convertsRowAndKeepsSharedObject()
    {
        var row = GivenThumbnail(isManual: false, "thumbnails/hash");

        await SetCoverAsync();

        row.IsManual.Should().BeTrue();
        row.ObjectKey.Should().StartWith($"track-covers/{_versionId}/");
        await _s3.DidNotReceive().DeleteObjectAsync(
            Arg.Any<string>(), "thumbnails/hash", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetCustomCoverAsync_existingManualCover_rotatesKeyAndDeletesPreviousObject()
    {
        var row = GivenThumbnail(isManual: true, "track-covers/old");

        await SetCoverAsync();

        row.ObjectKey.Should().NotBe("track-covers/old");
        await _s3.Received(1).DeleteObjectAsync(
            _config.PreviewBucket, "track-covers/old", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetCustomCoverAsync_previousObjectDeleteFails_stillSucceeds()
    {
        GivenThumbnail(isManual: true, "track-covers/old");
        _s3.DeleteObjectAsync(Arg.Any<string>(), "track-covers/old", Arg.Any<CancellationToken>())
            .ThrowsAsync(new AmazonS3Exception("unavailable"));

        var act = () => SetCoverAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SetCustomCoverAsync_commitFails_rollsBackAndDeletesNewObject()
    {
        var row = GivenThumbnail(isManual: true, "track-covers/old");
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new TimeoutException());

        var act = () => SetCoverAsync();

        await act.Should().ThrowAsync<TimeoutException>();
        await _unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _s3.Received(1).DeleteObjectAsync(_config.PreviewBucket, row.ObjectKey, Arg.Any<CancellationToken>());
        await _s3.DidNotReceive().DeleteObjectAsync(
            Arg.Any<string>(), "track-covers/old", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetCustomCoverAsync_foreignOwner_notFoundWithoutStore()
    {
        GivenVersion(ownerId: Guid.NewGuid());

        await ExpectRejectedWithoutStoreAsync<KeyNotFoundException>(() => SetCoverAsync());
    }

    [Fact]
    public async Task SetCustomCoverAsync_versionOfAnotherFile_notFoundWithoutStore()
    {
        await ExpectRejectedWithoutStoreAsync<KeyNotFoundException>(() =>
            _sut.SetCustomCoverAsync(Guid.NewGuid(), _versionId, _userId, new MemoryStream(Jpeg), Ct));
    }

    [Fact]
    public async Task SetCustomCoverAsync_deletedVersion_notFoundWithoutStore()
    {
        GivenVersion().DeletedAt = DateTime.UtcNow;

        await ExpectRejectedWithoutStoreAsync<KeyNotFoundException>(() => SetCoverAsync());
    }

    [Fact]
    public async Task SetCustomCoverAsync_nonAudioFile_rejectedWithoutStore()
    {
        GivenVersion(mimeType: "video/mp4");

        await ExpectRejectedWithoutStoreAsync<InvalidOperationException>(() => SetCoverAsync());
    }

    [Fact]
    public async Task SetCustomCoverAsync_encryptedVersion_rejectedWithoutStore()
    {
        GivenVersion(isEncrypted: true);

        await ExpectRejectedWithoutStoreAsync<InvalidOperationException>(() => SetCoverAsync());
    }

    [Fact]
    public async Task SetCustomCoverAsync_unpromotedVersion_rejectedWithoutStore()
    {
        GivenVersion(isPromoted: false);

        await ExpectRejectedWithoutStoreAsync<InvalidOperationException>(() => SetCoverAsync());
    }

    [Fact]
    public async Task SetCustomCoverAsync_oversizeImage_rejectedWithoutStore()
    {
        var oversize = new byte[4 * 1024 * 1024 + 1];
        Jpeg.CopyTo(oversize, 0);

        await ExpectRejectedWithoutStoreAsync<InvalidOperationException>(() => SetCoverAsync(oversize));
    }

    [Fact]
    public async Task SetCustomCoverAsync_unsupportedContent_rejectedWithoutStore()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>"u8.ToArray();

        await ExpectRejectedWithoutStoreAsync<InvalidOperationException>(() => SetCoverAsync(svg));
    }

    [Fact]
    public async Task UploadMediaData_manualCover_leavesThumbnailRowUntouched()
    {
        var row = GivenThumbnail(isManual: true, "track-covers/mine");
        var updatedAt = row.UpdatedAt;

        await UploadGeneratedAsync();

        row.ObjectKey.Should().Be("track-covers/mine");
        row.MimeType.Should().Be("image/jpeg");
        row.Size.Should().Be(10);
        row.UpdatedAt.Should().Be(updatedAt);
        await _previews.DidNotReceive().UpdateAsync(row, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadMediaData_automaticThumbnail_stillUpdatesRow()
    {
        var row = GivenThumbnail(isManual: false, "thumbnails/old");

        await UploadGeneratedAsync();

        row.ObjectKey.Should().Be("thumbnails/hash");
        row.Size.Should().Be(3);
        await _previews.Received(1).UpdateAsync(row, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeletePreviewsByFileAsync_manualCover_isKept()
    {
        var manual = GivenThumbnail(isManual: true, "track-covers/mine");
        var generated = GivenThumbnail(isManual: false, "thumbnails/hash");
        generated.Kind = PreviewKind.Preview;

        _files.GetByIdAsync(_fileId, Arg.Any<CancellationToken>())
            .Returns(new FileBuilder().WithId(_fileId).WithOwner(_userId).Build());
        _previews.GetByFileAsync(_fileId, null, Arg.Any<CancellationToken>())
            .Returns([manual, generated]);

        var (deletedCount, freedBytes) = await _sut.DeletePreviewsByFileAsync(_fileId, _userId, false, null, Ct);

        deletedCount.Should().Be(1);
        freedBytes.Should().Be(generated.Size);
        _previews.Received(1).Remove(generated);
        _previews.DidNotReceive().Remove(manual);
        await _s3.DidNotReceive().DeleteObjectAsync(
            Arg.Any<string>(), "track-covers/mine", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveCustomCoverAsync_manualCover_deletesRowAndObject()
    {
        var row = GivenThumbnail(isManual: true, "track-covers/mine");

        await _sut.RemoveCustomCoverAsync(_fileId, _versionId, _userId, Ct);

        _previews.Received(1).Remove(row);
        await _s3.Received(1).DeleteObjectAsync(
            _config.PreviewBucket, "track-covers/mine", Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveCustomCoverAsync_automaticThumbnail_changesNothing()
    {
        var row = GivenThumbnail(isManual: false, "thumbnails/hash");

        await _sut.RemoveCustomCoverAsync(_fileId, _versionId, _userId, Ct);

        _previews.DidNotReceive().Remove(row);
        await _s3.DidNotReceive().DeleteObjectAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveCustomCoverAsync_foreignOwner_notFound()
    {
        GivenVersion(ownerId: Guid.NewGuid());
        var row = GivenThumbnail(isManual: true, "track-covers/mine");

        var act = () => _sut.RemoveCustomCoverAsync(_fileId, _versionId, _userId, Ct);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        _previews.DidNotReceive().Remove(row);
    }

    [Fact]
    public async Task GetThumbnailContentPathAsync_manualCover_returnsItsKey()
    {
        GivenThumbnail(isManual: true, "track-covers/mine");

        var path = await _sut.GetThumbnailContentPathAsync(_versionId, "hash", Ct);

        path.Should().Be("track-covers/mine");
    }

    [Fact]
    public async Task GetThumbnailContentPathAsync_automaticThumbnail_fallsBackToHashPath()
    {
        GivenThumbnail(isManual: false, "thumbnails/hash");

        var path = await _sut.GetThumbnailContentPathAsync(_versionId, "hash", Ct);

        path.Should().Be("thumbnails/hash");
    }

    [Fact]
    public async Task GetCachedPreview_manualCover_addsCacheBuster()
    {
        var row = GivenThumbnail(isManual: true, "track-covers/mine");

        var path = await _sut.GetCachedPreview(_versionId, PreviewKind.Thumbnail, Ct);

        path.Should().Be($"/preview/{_versionId}/thumbnail?v={row.UpdatedAt!.Value.Ticks}");
    }

    [Fact]
    public async Task GetCachedPreview_automaticThumbnail_keepsPlainPath()
    {
        GivenThumbnail(isManual: false, "thumbnails/hash");

        var path = await _sut.GetCachedPreview(_versionId, PreviewKind.Thumbnail, Ct);

        path.Should().Be($"/preview/{_versionId}/thumbnail");
    }

    [Fact]
    public async Task GetCoverStampAsync_automaticThumbnail_returnsNull()
    {
        GivenThumbnail(isManual: false, "thumbnails/hash");

        var stamp = await _sut.GetCoverStampAsync(_versionId, Ct);

        stamp.Should().BeNull();
    }
}
