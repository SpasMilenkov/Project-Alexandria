using Alexandria.Common;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Services.User;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Alexandria.Tests.Unit.User;

public class UserAvatarServiceTests
{
    private readonly IStaticAssetService _assets = Substitute.For<IStaticAssetService>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _avatarId = Guid.NewGuid();

    private UserAvatarService CreateSut()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Users.Returns(_users);

        _assets.UploadAvatarAsync(Arg.Any<Stream>(), _userId, Arg.Any<CancellationToken>())
            .Returns(_avatarId);

        return new UserAvatarService(_assets, unitOfWork, NullLogger<UserAvatarService>.Instance);
    }

    [Fact]
    public async Task ReplaceAsync_liveAccount_pointsAtNewAvatarAndSweepsTheRest()
    {
        var sut = CreateSut();
        _users.SetAvatarAsync(_userId, _avatarId, Arg.Any<CancellationToken>()).Returns(true);

        var url = await sut.ReplaceAsync(new MemoryStream(), _userId, TestContext.Current.CancellationToken);

        url.Should().Be($"/avatars/{_userId}/{_avatarId}");
        await _assets.Received(1).DeleteAvatarsExceptAsync(_userId, _avatarId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplaceAsync_missingAccount_removesUploadAndThrows()
    {
        var sut = CreateSut();
        _users.SetAvatarAsync(_userId, _avatarId, Arg.Any<CancellationToken>()).Returns(false);

        var act = async () =>
            await sut.ReplaceAsync(new MemoryStream(), _userId, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UserNotFoundException>();
        await _assets.Received(1).DeleteAvatarsExceptAsync(_userId, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplaceAsync_sweepFails_stillReturnsTheNewAvatar()
    {
        var sut = CreateSut();
        _users.SetAvatarAsync(_userId, _avatarId, Arg.Any<CancellationToken>()).Returns(true);
        _assets.DeleteAvatarsExceptAsync(_userId, _avatarId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("bucket offline"));

        var url = await sut.ReplaceAsync(new MemoryStream(), _userId, TestContext.Current.CancellationToken);

        url.Should().EndWith(_avatarId.ToString());
    }

    [Fact]
    public async Task ReplaceAsync_rejectedImage_neverTouchesTheAccount()
    {
        var sut = CreateSut();
        _assets.UploadAvatarAsync(Arg.Any<Stream>(), _userId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("not an image"));

        var act = async () =>
            await sut.ReplaceAsync(new MemoryStream(), _userId, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _users.DidNotReceive().SetAvatarAsync(
            Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveAsync_liveAccount_clearsAvatarAndSweepsEverything()
    {
        var sut = CreateSut();
        _users.SetAvatarAsync(_userId, null, Arg.Any<CancellationToken>()).Returns(true);

        await sut.RemoveAsync(_userId, TestContext.Current.CancellationToken);

        await _assets.Received(1).DeleteAvatarsExceptAsync(_userId, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveAsync_missingAccount_throwsWithoutSweeping()
    {
        var sut = CreateSut();
        _users.SetAvatarAsync(_userId, null, Arg.Any<CancellationToken>()).Returns(false);

        var act = async () => await sut.RemoveAsync(_userId, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UserNotFoundException>();
        await _assets.DidNotReceive().DeleteAvatarsExceptAsync(
            Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }
}
