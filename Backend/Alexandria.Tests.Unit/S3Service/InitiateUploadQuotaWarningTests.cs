using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Config;
using Alexandria.Common.Queues;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Dto.Files;
using Alexandria.Dto.PreviewsStats;
using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.S3Service;

public class InitiateUploadQuotaWarningTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileRepository _files = Substitute.For<IFileRepository>();
    private readonly IPreviewRepository _previews = Substitute.For<IPreviewRepository>();

    private readonly IStreamingRepresentationRepository _representations =
        Substitute.For<IStreamingRepresentationRepository>();

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IUploadRepository _uploads = Substitute.For<IUploadRepository>();
    private readonly IAmazonS3 _publicS3 = Substitute.For<IAmazonS3>();
    private readonly Services.Storage.S3Service _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public InitiateUploadQuotaWarningTests()
    {
        _unitOfWork.Files.Returns(_files);
        _unitOfWork.Previews.Returns(_previews);
        _unitOfWork.StreamingRepresentations.Returns(_representations);
        _unitOfWork.Users.Returns(_users);
        _unitOfWork.Uploads.Returns(_uploads);

        var config = Substitute.For<IOptions<S3Config>>();
        config.Value.Returns(new S3Config());

        _uploads.AddAsync(Arg.Any<Upload>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Upload>());
        _publicS3.GetPreSignedURLAsync(Arg.Any<GetPreSignedUrlRequest>())
            .Returns("https://presigned/upload");

        _sut = new Services.Storage.S3Service(
            Substitute.For<IAmazonS3>(),
            _publicS3,
            _unitOfWork,
            config,
            Substitute.For<ILogger<Services.Storage.S3Service>>(),
            Substitute.For<IPromotionQueue>(),
            Substitute.For<IFileService>(),
            new AuditContext(),
            Substitute.For<IUserSettingsService>());
    }

    private void SeedUsage(long filesSize, long quota)
    {
        var ct = TestContext.Current.CancellationToken;
        _files.GetDeletedSizeAsync(_userId, ct).Returns(0L);
        _files.GetSizeByTypeAsync(_userId, ct)
            .Returns(new Dictionary<string, long> { ["video/mp4"] = filesSize });
        _files.GetOldFilesAsync(_userId, ct).Returns(new List<FileSummary>());
        _previews.GetStorageByUserAsync(_userId, ct)
            .Returns((0L, 0, new List<PreviewKindTotals>()));
        _representations.GetStorageByUserAsync(_userId, ct).Returns((0L, 0));
        _users.GetByIdAsync(_userId, ct)
            .Returns(new ApplicationUser { Name = "user", StorageQuota = quota });
    }

    [Fact]
    public async Task initiate_over_quota_still_succeeds_warn_only()
    {
        var ct = TestContext.Current.CancellationToken;
        SeedUsage(filesSize: 900L, quota: 1000L);

        var (uploadId, url) = await _sut.InitiateFileUpload(
            "video/mp4", Convert.ToHexString(new byte[32]), _userId, 200L, null, ct);

        uploadId.Should().NotBeEmpty();
        url.Should().Be("https://presigned/upload");
        await _uploads.Received(1).AddAsync(Arg.Any<Upload>(), ct);
    }

    [Fact]
    public async Task initiate_under_quota_succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        SeedUsage(filesSize: 100L, quota: ApplicationUser.DefaultStorageQuotaBytes);

        var (uploadId, _) = await _sut.InitiateFileUpload(
            "video/mp4", Convert.ToHexString(new byte[32]), _userId, 200L, null, ct);

        uploadId.Should().NotBeEmpty();
        await _uploads.Received(1).AddAsync(Arg.Any<Upload>(), ct);
    }
}