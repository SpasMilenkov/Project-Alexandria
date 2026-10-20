using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming.Playlist;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using AutoPlaylistSweepService = Alexandria.Services.Streaming.Playlists.AutoPlaylistSweepService;

namespace Alexandria.Tests.Unit.Streaming;

public class AutoPlaylistSweepTests
{
    private static readonly Guid OwnerOne = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OwnerTwo = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPlaylistRepository _playlists = Substitute.For<IPlaylistRepository>();
    private readonly IAutoPlaylistSyncService _sync = Substitute.For<IAutoPlaylistSyncService>();
    private readonly IServiceScopeFactory _scopeFactory = Substitute.For<IServiceScopeFactory>();

    public AutoPlaylistSweepTests()
    {
        _unitOfWork.Playlists.Returns(_playlists);

        _scopeFactory.CreateScope().Returns(_ =>
        {
            var provider = Substitute.For<IServiceProvider>();
            provider.GetService(typeof(IAutoPlaylistSyncService)).Returns(_sync);

            var scope = Substitute.For<IServiceScope>();
            scope.ServiceProvider.Returns(provider);

            return scope;
        });
    }

    private AutoPlaylistSweepService CreateSut() => new(
        _unitOfWork, _scopeFactory, NullLogger<AutoPlaylistSweepService>.Instance);

    [Fact]
    public async Task SweepAsync_sweeps_every_owner_and_aggregates()
    {
        _playlists.GetSyncOwnerIdsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { OwnerOne, OwnerTwo });
        _sync.SyncOwnerAsync(OwnerOne, Arg.Any<CancellationToken>())
            .Returns(new AutoPlaylistSyncResult(2, 3, 0, 1));
        _sync.SyncOwnerAsync(OwnerTwo, Arg.Any<CancellationToken>())
            .Returns(new AutoPlaylistSyncResult(1, 0, 1, 0));

        var result = await CreateSut().SweepAsync(TestContext.Current.CancellationToken);

        result.Should().Be(new AutoPlaylistSweepResult(2, 3, 3, 1, 1));
    }

    [Fact]
    public async Task SweepAsync_no_owners_returns_zeroes()
    {
        _playlists.GetSyncOwnerIdsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Guid>());

        var result = await CreateSut().SweepAsync(TestContext.Current.CancellationToken);

        result.Should().Be(new AutoPlaylistSweepResult(0, 0, 0, 0, 0));
        await _sync.DidNotReceive().SyncOwnerAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SweepAsync_failing_owner_does_not_skip_remaining_owners()
    {
        _playlists.GetSyncOwnerIdsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { OwnerOne, OwnerTwo });
        _sync.SyncOwnerAsync(OwnerOne, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AutoPlaylistSyncResult>(
                new InvalidOperationException("poisoned")));
        _sync.SyncOwnerAsync(OwnerTwo, Arg.Any<CancellationToken>())
            .Returns(new AutoPlaylistSyncResult(1, 2, 0, 0));

        var result = await CreateSut().SweepAsync(TestContext.Current.CancellationToken);

        await _sync.Received(1).SyncOwnerAsync(OwnerTwo, Arg.Any<CancellationToken>());
        result.Should().Be(new AutoPlaylistSweepResult(1, 1, 2, 0, 0));
    }

    [Fact]
    public async Task SweepAsync_resolves_sync_service_on_a_fresh_scope_per_owner()
    {
        _playlists.GetSyncOwnerIdsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { OwnerOne, OwnerTwo });
        _sync.SyncOwnerAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new AutoPlaylistSyncResult(0, 0, 0, 0));

        await CreateSut().SweepAsync(TestContext.Current.CancellationToken);

        _scopeFactory.Received(2).CreateScope();
        await _sync.Received(1).SyncOwnerAsync(OwnerOne, Arg.Any<CancellationToken>());
        await _sync.Received(1).SyncOwnerAsync(OwnerTwo, Arg.Any<CancellationToken>());
    }
}