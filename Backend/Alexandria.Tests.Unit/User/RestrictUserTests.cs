using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Services.User;
using AwesomeAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.User;

public class RestrictUserTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly ApplicationUser _user = new()
    {
        Id = Guid.NewGuid(),
        Name = "user",
        UserName = "user",
        Email = "user@example.com"
    };

    public RestrictUserTests()
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

        _unitOfWork.RefreshTokens.Returns(_refreshTokens);
        _userManager.FindByIdAsync(_user.Id.ToString()).Returns(_user);
    }

    [Fact]
    public async Task RestrictUserAsync_lockoutApplied_revokesRefreshSessions()
    {
        _userManager.SetLockoutEnabledAsync(_user, true).Returns(IdentityResult.Success);
        _userManager.SetLockoutEndDateAsync(_user, Arg.Any<DateTimeOffset?>()).Returns(IdentityResult.Success);

        await CreateSut().RestrictUserAsync(_user.Id, DateTime.UtcNow.AddDays(1), TestContext.Current.CancellationToken);

        await _refreshTokens.Received(1).RevokeUserTokensAsync(_user.Id, Arg.Any<CancellationToken>());
        _user.LockoutStartedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RestrictUserAsync_lockoutFails_keepsRefreshSessions()
    {
        _userManager.SetLockoutEnabledAsync(_user, true).Returns(IdentityResult.Success);
        _userManager.SetLockoutEndDateAsync(_user, Arg.Any<DateTimeOffset?>())
            .Returns(IdentityResult.Failed(new IdentityError { Description = "boom" }));

        var act = () => CreateSut().RestrictUserAsync(_user.Id, DateTime.UtcNow.AddDays(1),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _refreshTokens.DidNotReceiveWithAnyArgs().RevokeUserTokensAsync(default, default);
    }

    private UserManagementService CreateSut()
    {
        return new UserManagementService(_userManager, _unitOfWork, Substitute.For<IContentStorageService>());
    }
}
