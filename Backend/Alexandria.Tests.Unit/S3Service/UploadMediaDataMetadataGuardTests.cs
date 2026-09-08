using System.Linq.Expressions;
using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Config;
using Alexandria.Common.Queues;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Common.Settings.Values;
using Alexandria.Dto.Files;
using Alexandria.Tests.Common.Builders;
using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using MediaMetadataEntity = Alexandria.Data.Models.MediaMetadata;

namespace Alexandria.Tests.Unit.S3Service;

public class UploadMediaDataMetadataGuardTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileVersionRepository _versions = Substitute.For<IFileVersionRepository>();
    private readonly IMediaMetadataRepository _metadata = Substitute.For<IMediaMetadataRepository>();
    private readonly IPreviewRepository _previews = Substitute.For<IPreviewRepository>();
    private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();
    private readonly IUserSettingsService _settings = Substitute.For<IUserSettingsService>();
    private readonly Services.Storage.S3Service _sut;
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _versionId = Guid.NewGuid();
    private readonly Guid _fileId = Guid.NewGuid();

    public UploadMediaDataMetadataGuardTests()
    {
        _unitOfWork.FileVersions.Returns(_versions);
        _unitOfWork.MediaMetadata.Returns(_metadata);
        _unitOfWork.Previews.Returns(_previews);

        var config = Substitute.For<IOptions<S3Config>>();
        config.Value.Returns(new S3Config());

        _s3.PutObjectAsync(
                Arg.Any<PutObjectRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse());

        _sut = new Services.Storage.S3Service(
            _s3,
            Substitute.For<IAmazonS3>(),
            _unitOfWork,
            config,
            Substitute.For<ILogger<Services.Storage.S3Service>>(),
            Substitute.For<IPromotionQueue>(),
            Substitute.For<IFileService>(),
            new AuditContext(),
            _settings);
    }

    private void GivenVersionWithOwner()
    {
        var file = new FileBuilder()
            .WithId(_fileId)
            .WithOwner(_ownerId)
            .Build();
        var version = new FileVersionBuilder()
            .WithFileId(_fileId)
            .Build();
        version.File = file;
        _versions.GetByIdAsync(_versionId, Arg.Any<CancellationToken>()).Returns(version);
    }

    private static MediaMetadataEntity ExistingMetadata(Guid fileId, string? album) => new()
    {
        FileId = fileId,
        Title = "Existing Title",
        Artist = "Existing Artist",
        Album = album,
    };

    private static MediaMetadataDto IncomingDto() => new()
    {
        Duration = 180,
        Title = "Incoming Title",
        Artist = "Incoming Artist",
        Album = "Incoming Album",
    };

    private void GivenExistingMetadata(MediaMetadataEntity metadata)
    {
        _metadata
            .FirstOrDefaultAsync(
                Arg.Any<Expression<Func<MediaMetadataEntity, bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(metadata);
    }

    private void GivenOverwriteFlag(bool allow)
    {
        _settings
            .GetBehaviorAsync(_ownerId, Arg.Any<CancellationToken>())
            .Returns(new BehaviorSettingsValue { AllowAutomaticMetadataOverwrite = allow });
    }

    private Task CallUploadAsync(MediaMetadataDto dto)
    {
        var ct = TestContext.Current.CancellationToken;
        using var preview = new MemoryStream([1, 2, 3]);
        using var thumbnail = new MemoryStream([4, 5, 6]);
        return _sut.UploadMediaData(
            preview, thumbnail, 3, 3, "object", _versionId, dto, ct);
    }

    [Fact]
    public async Task UploadMediaData_flagOff_preservesNonEmptyDescriptiveFields()
    {
        GivenVersionWithOwner();
        var existing = ExistingMetadata(_fileId, "User Album");
        GivenExistingMetadata(existing);
        GivenOverwriteFlag(false);

        await CallUploadAsync(IncomingDto());

        existing.Album.Should().Be("User Album");
        existing.Title.Should().Be("Existing Title");
        existing.Artist.Should().Be("Existing Artist");
        await _settings.Received(1).GetBehaviorAsync(_ownerId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadMediaData_flagOn_overwritesDescriptiveFields()
    {
        GivenVersionWithOwner();
        var existing = ExistingMetadata(_fileId, "User Album");
        GivenExistingMetadata(existing);
        GivenOverwriteFlag(true);

        await CallUploadAsync(IncomingDto());

        existing.Album.Should().Be("Incoming Album");
        existing.Title.Should().Be("Incoming Title");
        existing.Artist.Should().Be("Incoming Artist");
    }

    [Fact]
    public async Task UploadMediaData_flagOff_fillsEmptyFields()
    {
        GivenVersionWithOwner();
        var existing = ExistingMetadata(_fileId, null);
        GivenExistingMetadata(existing);
        GivenOverwriteFlag(false);

        await CallUploadAsync(IncomingDto());

        existing.Album.Should().Be("Incoming Album");
    }
}