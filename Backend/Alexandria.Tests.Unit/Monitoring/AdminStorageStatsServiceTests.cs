using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Services.Monitoring;
using AwesomeAssertions;
using NSubstitute;

namespace Alexandria.Tests.Unit.Monitoring;

public class AdminStorageStatsServiceTests
{
    private readonly IFileRepository _files = Substitute.For<IFileRepository>();
    private readonly IPreviewRepository _previews = Substitute.For<IPreviewRepository>();

    private readonly IStreamingRepresentationRepository _representations =
        Substitute.For<IStreamingRepresentationRepository>();

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IAdminStorageStatsService _sut;
    private readonly Guid _user1 = Guid.NewGuid();
    private readonly Guid _user2 = Guid.NewGuid();

    public AdminStorageStatsServiceTests()
    {
        _sut = new AdminStorageStatsService(_files, _previews, _representations, _users);
    }

    private void SeedSizes(
        Dictionary<Guid, long> files,
        Dictionary<Guid, long> previews,
        Dictionary<Guid, long> transcoded,
        long trash = 0L)
    {
        var ct = TestContext.Current.CancellationToken;
        _files.GetLiveSizeByOwnerAsync(ct).Returns(files);
        _previews.GetSizeByOwnerAsync(ct).Returns(previews);
        _representations.GetSizeByOwnerAsync(ct).Returns(transcoded);
        _files.GetTotalTrashSizeAsync(ct).Returns(trash);
    }

    private static ApplicationUser User(Guid id, string name, long quota)
        => new()
        {
            Id = id,
            Name = name,
            UserName = name,
            Email = $"{name}@example.com",
            StorageQuota = quota
        };

    [Fact]
    public async Task split_sums_across_owners()
    {
        var ct = TestContext.Current.CancellationToken;
        SeedSizes(
            new Dictionary<Guid, long> { [_user1] = 100L, [_user2] = 200L },
            new Dictionary<Guid, long> { [_user1] = 10L },
            new Dictionary<Guid, long> { [_user2] = 30L },
            trash: 5L);

        var result = await _sut.GetSplitAsync(ct);

        result.FilesSize.Should().Be(300L);
        result.PreviewsSize.Should().Be(10L);
        result.TranscodedSize.Should().Be(30L);
        result.TrashSize.Should().Be(5L);
    }

    [Fact]
    public async Task ranking_orders_takes_top_and_excludes_zero_usage()
    {
        var ct = TestContext.Current.CancellationToken;
        SeedSizes(
            new Dictionary<Guid, long> { [_user1] = 100L, [_user2] = 500L },
            new Dictionary<Guid, long> { [_user1] = 50L },
            new Dictionary<Guid, long>());
        var idle = Guid.NewGuid();
        _users.FindAsync(Arg.Any<Expression<Func<ApplicationUser, bool>>>(), ct)
            .Returns(new List<ApplicationUser>
            {
                User(_user1, "one", 10_000L),
                User(_user2, "two", 20_000L),
                User(idle, "idle", 0L),
            });

        var result = await _sut.GetUserRankingAsync(2, ct);

        result.Should().HaveCount(2);
        result[0].UserId.Should().Be(_user2);
        result[0].UsedBytes.Should().Be(500L);
        result[0].QuotaBytes.Should().Be(20_000L);
        result[1].UserId.Should().Be(_user1);
        result[1].UsedBytes.Should().Be(150L);
        result[1].FilesSize.Should().Be(100L);
        result[1].PreviewsSize.Should().Be(50L);
        result[1].TranscodedSize.Should().Be(0L);
    }

    [Fact]
    public async Task ranking_empty_when_no_usage()
    {
        var ct = TestContext.Current.CancellationToken;
        SeedSizes(new Dictionary<Guid, long>(), new Dictionary<Guid, long>(),
            new Dictionary<Guid, long>());
        _users.FindAsync(Arg.Any<Expression<Func<ApplicationUser, bool>>>(), ct)
            .Returns(new List<ApplicationUser> { User(_user1, "one", 10_000L) });

        var result = await _sut.GetUserRankingAsync(10, ct);

        result.Should().BeEmpty();
    }
}