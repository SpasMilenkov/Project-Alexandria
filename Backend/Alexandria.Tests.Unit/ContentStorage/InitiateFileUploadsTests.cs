using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Config;
using Alexandria.Common.Queues;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Dto.Files;
using Alexandria.Services.Storage.Content;
using Alexandria.Services.Storage.Content.Mime;
using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Alexandria.Tests.Unit.ContentStorage;

public class InitiateFileUploadsTests
{
    private static readonly string ValidHash = new('a', 64);

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileRepository _files = Substitute.For<IFileRepository>();
    private readonly IUploadRepository _uploads = Substitute.For<IUploadRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IFileService _fileService = Substitute.For<IFileService>();
    private readonly IAmazonS3 _publicS3 = Substitute.For<IAmazonS3>();
    private readonly List<GetPreSignedUrlRequest> _signed = [];
    private readonly ILogger<ContentStorageService> _logger = Substitute.For<ILogger<ContentStorageService>>();
    private readonly Guid _userId = Guid.NewGuid();

    private ContentStorageService CreateSut()
    {
        _unitOfWork.Files.Returns(_files);
        _unitOfWork.Uploads.Returns(_uploads);
        _unitOfWork.Users.Returns(_users);
        _unitOfWork.Previews.Returns(Substitute.For<IPreviewRepository>());
        _unitOfWork.StreamingRepresentations.Returns(Substitute.For<IStreamingRepresentationRepository>());

        _publicS3.GetPreSignedURLAsync(Arg.Do<GetPreSignedUrlRequest>(_signed.Add))
            .Returns("https://presigned/upload");

        var config = Substitute.For<IOptions<S3Config>>();
        config.Value.Returns(new S3Config());

        return new ContentStorageService(
            Substitute.For<IAmazonS3>(),
            _publicS3,
            _unitOfWork,
            config,
            _logger,
            Substitute.For<IPromotionQueue>(),
            _fileService,
            new AuditContext(),
            Substitute.For<IUserSettingsService>(),
            Substitute.For<IPublisherService>(),
            new MimeTypeResolver(Substitute.For<IMimeSniffer>()));
    }

    private static UploadInitItem Item(long size = 10, Guid? directoryId = null) => new()
    {
        ContentType = "text/plain",
        Hash = ValidHash,
        ContentLength = size,
        DirectoryId = directoryId
    };

    [Fact]
    public async Task InitiateFileUploads_signs_every_item_for_the_incoming_prefix_with_a_short_lifetime()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();
        var before = DateTime.UtcNow;

        var results = await sut.InitiateFileUploads([Item(), Item()], _userId, ct);

        results.Should().OnlyContain(r => r.UploadId != null && r.UploadUrl != null && r.Error == null);
        results[0].UploadId.Should().NotBe(results[1].UploadId!.Value);

        _signed.Should().HaveCount(2);
        _signed.Should().OnlyContain(r => r.Key.StartsWith(TempObjectKeys.Incoming));
        _signed.Should().OnlyContain(r => r.Verb == HttpVerb.PUT && r.ContentType == "text/plain");
        _signed.Should().OnlyContain(r => r.Expires!.Value <= before.AddMinutes(3));
    }

    [Fact]
    public async Task InitiateFileUploads_runs_one_quota_check_against_the_summed_size()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();

        await sut.InitiateFileUploads([Item(size: 10), Item(size: 20), Item(size: 30)], _userId, ct);

        await _files.Received(1).GetSizeByTypeAsync(_userId, ct);
    }

    [Fact]
    public async Task InitiateFileUploads_inserts_the_batch_in_one_transaction()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();

        await sut.InitiateFileUploads([Item(), Item(), Item()], _userId, ct);

        await _unitOfWork.Received(1).BeginTransactionAsync(ct);
        await _unitOfWork.Received(1).CommitAsync(ct);
        await _uploads.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<Upload>>(rows => rows.Count() == 3), ct);
    }

    [Fact]
    public async Task InitiateFileUploads_unavailable_directory_fails_only_its_own_items()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();
        var bad = Guid.NewGuid();
        var good = Guid.NewGuid();

        _fileService.FolderWithOwnershipExistsAsync(bad, _userId, ct)
            .ThrowsAsync(new DirectoryNotFoundException());

        var results = await sut.InitiateFileUploads(
            [Item(directoryId: good), Item(directoryId: bad), Item(), Item(directoryId: bad)],
            _userId,
            ct);

        results.Select(r => r.Error is null).Should().Equal(true, false, true, false);
        results[1].Error.Should().Be(UploadErrorMessages.DestinationNotFound);
        results[1].UploadUrl.Should().BeNull();

        await _uploads.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<Upload>>(rows => rows.Count() == 2), ct);

        // Two items share the bad directory, so distinct directories are only checked once.
        await _fileService.Received(1).FolderWithOwnershipExistsAsync(bad, _userId, ct);
    }

    [Fact]
    public async Task InitiateFileUploads_every_directory_unavailable_opens_no_transaction()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();
        var bad = Guid.NewGuid();

        _fileService.FolderWithOwnershipExistsAsync(bad, _userId, ct)
            .ThrowsAsync(new DirectoryNotFoundException());

        var results = await sut.InitiateFileUploads([Item(directoryId: bad)], _userId, ct);

        results[0].Error.Should().Be(UploadErrorMessages.DestinationNotFound);
        await _unitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitiateFileUploads_insert_fails_rolls_back_and_propagates()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();

        _uploads.AddRangeAsync(Arg.Any<IEnumerable<Upload>>(), ct)
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var act = () => sut.InitiateFileUploads([Item()], _userId, ct);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _unitOfWork.Received(1).RollbackAsync(CancellationToken.None);
    }

    [Fact]
    public async Task InitiateFileUpload_single_path_also_signs_for_the_incoming_prefix()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();

        _uploads.AddAsync(Arg.Any<Upload>(), ct).Returns(ci => ci.Arg<Upload>());

        await sut.InitiateFileUpload("text/plain", ValidHash, _userId, 10, null, ct);

        _signed.Should().ContainSingle()
            .Which.Key.Should().StartWith(TempObjectKeys.Incoming);
    }
}
