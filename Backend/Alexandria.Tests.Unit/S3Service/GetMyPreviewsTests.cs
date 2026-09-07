using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Config;
using Alexandria.Common.Queues;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Previews;
using Amazon.S3;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.S3Service;

public class GetMyPreviewsTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPreviewRepository _previews = Substitute.For<IPreviewRepository>();
    private readonly Services.Storage.S3Service _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public GetMyPreviewsTests()
    {
        _unitOfWork.Previews.Returns(_previews);

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

    [Fact]
    public async Task delegates_filters_paging_and_returns_page()
    {
        var ct = TestContext.Current.CancellationToken;
        var fileId = Guid.NewGuid();
        var cutoff = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var page = new PaginatedResult<UserPreviewDto>
        {
            Items =
            [
                new(Guid.NewGuid(), fileId, "photo.png", PreviewKind.Thumbnail, 512L,
                    cutoff.AddDays(-1)),
                new(Guid.NewGuid(), fileId, "photo.png", PreviewKind.Preview, 1024L,
                    cutoff.AddDays(-2)),
            ],
            TotalCount = 2,
            CurrentPage = 1,
            PageSize = 20,
            TotalPages = 1
        };
        _previews.GetListByUserAsync(_userId, fileId, cutoff, 1, 20, ct).Returns(page);

        var result = await _sut.GetMyPreviewsAsync(_userId, fileId, cutoff, 1, 20, ct);

        result.Should().BeEquivalentTo(page);
        await _previews.Received(1).GetListByUserAsync(_userId, fileId, cutoff, 1, 20, ct);
    }
}