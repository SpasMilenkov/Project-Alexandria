using Alexandria.Common;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Dto.Users;
using Alexandria.Services.User;
using AwesomeAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.User;

public class UpdateProfileTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly Guid _userId = Guid.NewGuid();

    public UpdateProfileTests()
    {
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<IPasswordHasher<ApplicationUser>>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            Substitute.For<ILookupNormalizer>(),
            Substitute.For<IdentityErrorDescriber>(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<ApplicationUser>>>());
    }

    private UserManagementService CreateSut()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Users.Returns(_users);

        _users.GetUserProfileAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new UserProfileDto
            {
                Id = _userId,
                Username = "renamed",
                Email = "user@example.com",
                CreatedAt = DateTime.UtcNow,
            });

        return new UserManagementService(_userManager, unitOfWork, Substitute.For<IContentStorageService>());
    }

    private ApplicationUser StoredUser(DateTime? deletedAt = null)
    {
        var user = new ApplicationUser
        {
            Id = _userId,
            Name = "original",
            UserName = "original",
            Email = "user@example.com",
            CreatedAt = DateTime.UtcNow,
            DeletedAt = deletedAt,
        };

        _userManager.FindByIdAsync(_userId.ToString()).Returns(user);

        return user;
    }

    [Fact]
    public async Task UpdateProfileAsync_newName_setsTrimmedUserName()
    {
        var sut = CreateSut();
        var user = StoredUser();
        _userManager.SetUserNameAsync(user, "renamed").Returns(IdentityResult.Success);

        var profile = await sut.UpdateProfileAsync(
            _userId, new UpdateProfileDto { UserName = "  renamed " }, TestContext.Current.CancellationToken);

        profile.Username.Should().Be("renamed");
        user.UpdatedBy.Should().Be(_userId);
        await _userManager.Received(1).SetUserNameAsync(user, "renamed");
    }

    [Fact]
    public async Task UpdateProfileAsync_sameName_skipsTheWrite()
    {
        var sut = CreateSut();
        StoredUser();

        await sut.UpdateProfileAsync(
            _userId, new UpdateProfileDto { UserName = "original" }, TestContext.Current.CancellationToken);

        await _userManager.DidNotReceive().SetUserNameAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    [Fact]
    public async Task UpdateProfileAsync_duplicateName_throwsUserNameTaken()
    {
        var sut = CreateSut();
        var user = StoredUser();
        _userManager.SetUserNameAsync(user, "taken").Returns(IdentityResult.Failed(new IdentityError
        {
            Code = nameof(IdentityErrorDescriber.DuplicateUserName),
            Description = "Username 'taken' is already taken.",
        }));

        var act = async () => await sut.UpdateProfileAsync(
            _userId, new UpdateProfileDto { UserName = "taken" }, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UserNameTakenException>();
    }

    [Fact]
    public async Task UpdateProfileAsync_deletedAccount_throwsUserNotFound()
    {
        var sut = CreateSut();
        StoredUser(deletedAt: DateTime.UtcNow);

        var act = async () => await sut.UpdateProfileAsync(
            _userId, new UpdateProfileDto { UserName = "renamed" }, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    [Fact]
    public async Task GetProfileStatsAsync_goneAccount_throwsUserNotFound()
    {
        var sut = CreateSut();
        _users.GetProfileStatsAsync(_userId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns((UserProfileStatsDto?)null);

        var act = async () => await sut.GetProfileStatsAsync(_userId, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    [Fact]
    public async Task GetProfileStatsAsync_countsShareLinksAgainstTheCurrentTime()
    {
        var sut = CreateSut();
        var before = DateTime.UtcNow;
        _users.GetProfileStatsAsync(_userId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new UserProfileStatsDto
            {
                Files = 0,
                Folders = 0,
                Photos = 0,
                Tracks = 0,
                Videos = 0,
                Playlists = 0,
                Tags = 0,
                ActiveShareLinks = 0,
                ListenedSeconds = 0,
                Plays = 0,
            });

        await sut.GetProfileStatsAsync(_userId, TestContext.Current.CancellationToken);

        await _users.Received(1).GetProfileStatsAsync(
            _userId,
            Arg.Is<DateTime>(now => now >= before && now.Kind == DateTimeKind.Utc),
            Arg.Any<CancellationToken>());
    }
}
