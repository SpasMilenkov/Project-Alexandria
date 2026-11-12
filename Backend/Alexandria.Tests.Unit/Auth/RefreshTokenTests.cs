using Alexandria.Api.Features.Auth;
using Alexandria.Common;
using Alexandria.Common.Audit;
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
    private readonly IAuditActorAccessor _auditActor = Substitute.For<IAuditActorAccessor>();

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
        _refreshTokens.RevokeIfActiveAsync(storedToken.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await CreateSut().RefreshTokenAsync(TokenValue, TestContext.Current.CancellationToken);

        result.Succeeded.Should().BeTrue();
        result.RefreshToken.Should().NotBeNullOrEmpty().And.NotBe(TokenValue);
        await _refreshTokens.Received(1).AddAsync(
            Arg.Is<RefreshToken>(t => t.SessionStartedAt == storedToken.SessionStartedAt),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshTokenAsync_lostTheRaceToAnotherTab_returnsTheSessionsCurrentToken()
    {
        var storedToken = StoredToken(CreateUser());
        var successor = Successor(storedToken);
        _refreshTokens.RevokeIfActiveAsync(storedToken.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await CreateSut().RefreshTokenAsync(TokenValue, TestContext.Current.CancellationToken);

        result.Succeeded.Should().BeTrue();
        result.RefreshToken.Should().Be(successor.Token);
        await _refreshTokens.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task RefreshTokenAsync_justRotatedToken_returnsTheSessionsCurrentToken()
    {
        var storedToken = StoredToken(CreateUser());
        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow.AddSeconds(-5);
        var successor = Successor(storedToken);

        var result = await CreateSut().RefreshTokenAsync(TokenValue, TestContext.Current.CancellationToken);

        result.Succeeded.Should().BeTrue();
        result.RefreshToken.Should().Be(successor.Token);
        await _refreshTokens.DidNotReceiveWithAnyArgs().RevokeIfActiveAsync(default, default, default);
        await _refreshTokens.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task RefreshTokenAsync_tokenSpentLongAgo_failsWithoutLookingForASuccessor()
    {
        var storedToken = StoredToken(CreateUser());
        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow.AddMinutes(-2);
        Successor(storedToken);

        var result = await CreateSut().RefreshTokenAsync(TokenValue, TestContext.Current.CancellationToken);

        result.Succeeded.Should().BeFalse();
        await _refreshTokens.DidNotReceiveWithAnyArgs().GetLiveSuccessorAsync(default, default, default, default, default);
    }

    [Fact]
    public async Task RefreshTokenAsync_signedOutSession_failsEvenWithinTheGracePeriod()
    {
        var storedToken = StoredToken(CreateUser());
        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow.AddSeconds(-1);
        _refreshTokens.GetLiveSuccessorAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(),
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns((RefreshToken?)null);

        var result = await CreateSut().RefreshTokenAsync(TokenValue, TestContext.Current.CancellationToken);

        result.Succeeded.Should().BeFalse();
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

        return new AuthService(_userManager, null!, _unitOfWork, config, Substitute.For<IAuditWriter>(),
            _auditActor);
    }

    private RefreshToken StoredToken(ApplicationUser user)
    {
        var storedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = TokenValue,
            UserId = user.Id,
            User = user,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            SessionStartedAt = DateTime.UtcNow.AddDays(-3),
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
        };

        _refreshTokens.GetByTokenWithUserAsync(TokenValue, Arg.Any<CancellationToken>()).Returns(storedToken);

        return storedToken;
    }

    private RefreshToken Successor(RefreshToken spent)
    {
        var successor = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = "successor-token",
            UserId = spent.UserId,
            SessionStartedAt = spent.SessionStartedAt,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        };

        _refreshTokens.GetLiveSuccessorAsync(spent.UserId, spent.SessionStartedAt, spent.CreatedAt,
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(successor);

        return successor;
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
