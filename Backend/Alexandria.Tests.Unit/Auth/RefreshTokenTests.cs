using Alexandria.Api.Features.Auth;
using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Data.Models;
using AwesomeAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.Auth;

public class RefreshTokenTests
{
    private const string TokenValue = "stored-token";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();

    public RefreshTokenTests()
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
    }

    [Fact]
    public async Task RefreshTokenAsync_activeUser_rotatesToken()
    {
        var storedToken = StoredToken(CreateUser());
        _userManager.IsLockedOutAsync(storedToken.User!).Returns(false);

        var result = await CreateSut().RefreshTokenAsync(TokenValue, TestContext.Current.CancellationToken);

        result.Succeeded.Should().BeTrue();
        result.RefreshToken.Should().NotBeNullOrEmpty().And.NotBe(TokenValue);
        storedToken.IsRevoked.Should().BeTrue();
        await _refreshTokens.Received(1).AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshTokenAsync_lockedOutUser_failsWithoutIssuingToken()
    {
        var storedToken = StoredToken(CreateUser());
        _userManager.IsLockedOutAsync(storedToken.User!).Returns(true);

        var result = await CreateSut().RefreshTokenAsync(TokenValue, TestContext.Current.CancellationToken);

        result.Succeeded.Should().BeFalse();
        await _refreshTokens.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task RefreshTokenAsync_deletedUser_failsWithoutLockoutLookup()
    {
        var user = CreateUser();
        user.DeletedAt = DateTime.UtcNow;
        StoredToken(user);

        var result = await CreateSut().RefreshTokenAsync(TokenValue, TestContext.Current.CancellationToken);

        result.Succeeded.Should().BeFalse();
        await _userManager.DidNotReceiveWithAnyArgs().IsLockedOutAsync(default!);
        await _refreshTokens.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task RefreshTokenAsync_unknownToken_fails()
    {
        _refreshTokens.GetByTokenWithUserAsync(TokenValue, Arg.Any<CancellationToken>()).Returns((RefreshToken?)null);

        var result = await CreateSut().RefreshTokenAsync(TokenValue, TestContext.Current.CancellationToken);

        result.Succeeded.Should().BeFalse();
    }

    private AuthService CreateSut()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();

        return new AuthService(_userManager, null!, _unitOfWork, config);
    }

    private RefreshToken StoredToken(ApplicationUser user)
    {
        var storedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = TokenValue,
            UserId = user.Id,
            User = user,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };

        _refreshTokens.GetByTokenWithUserAsync(TokenValue, Arg.Any<CancellationToken>()).Returns(storedToken);

        return storedToken;
    }

    private static ApplicationUser CreateUser()
    {
        return new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            Name = "User",
            UserName = "user"
        };
    }
}
