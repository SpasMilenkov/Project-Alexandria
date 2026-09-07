using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Config;
using Alexandria.Common.Queues;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.PreviewsStats;
using Amazon.S3;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.S3Service;

public class GetStorageBreakdownTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileRepository _files = Substitute.For<IFileRepository>();
    private readonly IPreviewRepository _previews = Substitute.For<IPreviewRepository>();

    private readonly IStreamingRepresentationRepository _representations =
        Substitute.For<IStreamingRepresentationRepository>();

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly Services.Storage.S3Service _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public GetStorageBreakdownTests()
    {
        _unitOfWork.Files.Returns(_files);
        _unitOfWork.Previews.Returns(_previews);
        _unitOfWork.StreamingRepresentations.Returns(_representations);
        _unitOfWork.Users.Returns(_users);

        _sut = new Services.Storage.S3Service(
            Substitute.For<IAmazonS3>(),
            Substitute.For<IAmazonS3>(),
            _unitOfWork,
            Substitute.For<IOptions<S3Config>>(),
            Substitute.For<ILogger<Services.Storage.S3Service>>(),
            Substitute.For<IPromotionQueue>(),
            Substitute.For<IFileService>(),
            new AuditContext());
    }

    private void SeedUsage(
        Dictionary<string, long> sizeByType,
        long previewsSize,
        int previewsCount,
        IReadOnlyList<PreviewKindTotals> byKind,
        long transcodedSize,
        int representationsCount,
        ApplicationUser? user)
    {
        var ct = TestContext.Current.CancellationToken;
        _files.GetDeletedSizeAsync(_userId, ct).Returns(50L);
        _files.GetSizeByTypeAsync(_userId, ct).Returns(sizeByType);
        _files.GetOldFilesAsync(_userId, ct)
            .Returns(new List<FileSummary> { new(Guid.NewGuid(), "old.txt", "text/plain") });
        _previews.GetStorageByUserAsync(_userId, ct).Returns((previewsSize, previewsCount, byKind));
        _representations.GetStorageByUserAsync(_userId, ct).Returns((transcodedSize, representationsCount));
        _users.GetByIdAsync(_userId, ct).Returns(user);
    }

    [Fact]
    public async Task aggregates_files_previews_transcoded_and_quota_into_used()
    {
        var byKind = new List<PreviewKindTotals>
        {
            new(PreviewKind.Thumbnail, 2, 300L),
            new(PreviewKind.Preview, 1, 700L),
        };
        SeedUsage(
            new Dictionary<string, long> { ["image/png"] = 400L, ["video/mp4"] = 600L },
            1000L, 3, byKind, 5000L, 4,
            new ApplicationUser { Name = "user", StorageQuota = 10_737_418_240L });

        var result = await _sut.GetStorageBreakdown(_userId, TestContext.Current.CancellationToken);

        result.FilesSize.Should().Be(1000L);
        result.PreviewsSize.Should().Be(1000L);
        result.PreviewsCount.Should().Be(3);
        result.PreviewsByKind.Should().BeEquivalentTo(byKind);
        result.TranscodedSize.Should().Be(5000L);
        result.RepresentationsCount.Should().Be(4);
        result.QuotaBytes.Should().Be(10_737_418_240L);
        result.UsedBytes.Should().Be(7000L);
        result.TrashSize.Should().Be(50L);
        result.SizeByType.Should().ContainKey("image/png");
    }

    [Fact]
    public async Task missing_user_reports_zero_quota_but_still_sums_used()
    {
        SeedUsage(
            new Dictionary<string, long> { ["image/png"] = 200L },
            100L, 1,
            new List<PreviewKindTotals> { new(PreviewKind.Thumbnail, 1, 100L) },
            300L, 2, null);

        var result = await _sut.GetStorageBreakdown(_userId, TestContext.Current.CancellationToken);

        result.QuotaBytes.Should().Be(0L);
        result.UsedBytes.Should().Be(600L);
    }

    [Fact]
    public async Task empty_usage_reports_zero_used()
    {
        SeedUsage(
            new Dictionary<string, long>(),
            0L, 0, new List<PreviewKindTotals>(), 0L, 0,
            new ApplicationUser { Name = "user", StorageQuota = 10_737_418_240L });

        var result = await _sut.GetStorageBreakdown(_userId, TestContext.Current.CancellationToken);

        result.FilesSize.Should().Be(0L);
        result.UsedBytes.Should().Be(0L);
        result.PreviewsByKind.Should().BeEmpty();
    }
}