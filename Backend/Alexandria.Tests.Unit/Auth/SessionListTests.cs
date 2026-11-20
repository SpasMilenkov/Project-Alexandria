using System.Security.Claims;
using Alexandria.Api.Features.Auth;
using Alexandria.Api.Features.Auth.Extensions;
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

public class SessionListTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly Guid _userId = Guid.NewGuid();

    public SessionListTests()
    {
        _unitOfWork.RefreshTokens.Returns(_refreshTokens);
    }

    [Fact]
    public async Task GetSessionsAsync_sessionNamedByTheAccessToken_isTheCurrentOne()
    {
        var browser = ActiveToken("browser");
        var phone = ActiveToken("phone");
        _refreshTokens.GetActiveByUserAsync(_userId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([browser, phone]);

        var sessions = await CreateSut().GetSessionsAsync(_userId, phone.SessionId,
            TestContext.Current.CancellationToken);

        sessions.Single(s => s.IsCurrent).Id.Should().Be(phone.SessionId);
    }

    [Fact]
    public async Task GetSessionsAsync_listsSessionsByTheirStableId_notTheTokenRow()
    {
        var browser = ActiveToken("browser");
        _refreshTokens.GetActiveByUserAsync(_userId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([browser]);

        var sessions = await CreateSut().GetSessionsAsync(_userId, null, TestContext.Current.CancellationToken);

        sessions.Should().ContainSingle().Which.Id.Should().Be(browser.SessionId);
    }

    [Fact]
    public async Task GetSessionsAsync_tokenWithoutASession_marksNone()
    {
        _refreshTokens.GetActiveByUserAsync(_userId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([ActiveToken("browser")]);

        var sessions = await CreateSut().GetSessionsAsync(_userId, null, TestContext.Current.CancellationToken);

        sessions.Should().ContainSingle().Which.IsCurrent.Should().BeFalse();
    }

    [Fact]
    public void GetSessionId_readsTheSessionClaim_orNullWithoutOne()
    {
        var sessionId = Guid.NewGuid();
        var named = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Sid, sessionId.ToString())]));
        var older = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, _userId.ToString())]));

        named.GetSessionId().Should().Be(sessionId);
        older.GetSessionId().Should().BeNull();
    }

    private RefreshToken ActiveToken(string token)
    {
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            Token = token,
            UserId = _userId,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            SessionStartedAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
        };
    }

    private AuthService CreateSut()
    {
        var userManager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<IPasswordHasher<ApplicationUser>>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            Substitute.For<ILookupNormalizer>(),
            Substitute.For<IdentityErrorDescriber>(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<ApplicationUser>>>());
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();

        return new AuthService(userManager, null!, _unitOfWork, config, Substitute.For<IAuditWriter>(),
            Substitute.For<IAuditActorAccessor>());
    }
}