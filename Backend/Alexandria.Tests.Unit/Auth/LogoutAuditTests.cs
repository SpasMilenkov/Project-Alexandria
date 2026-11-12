using Alexandria.Api.Features.Auth;
using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Repositories;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.Auth;

public class LogoutAuditTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IAuditWriter _auditWriter = Substitute.For<IAuditWriter>();
    private readonly Guid _userId = Guid.NewGuid();

    public LogoutAuditTests()
    {
        _unitOfWork.RefreshTokens.Returns(_refreshTokens);
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

        return new AuthService(userManager, null!, _unitOfWork,
            new ConfigurationBuilder().AddInMemoryCollection().Build(), _auditWriter,
            Substitute.For<IAuditActorAccessor>());
    }

    [Fact]
    public async Task LogoutAsync_withToken_recordsBeforeRevokeSaves()
    {
        _refreshTokens.RevokeTokenAsync("token", Arg.Any<CancellationToken>()).Returns(true);

        await CreateSut().LogoutAsync(_userId, "token", TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            _auditWriter.Record(Arg.Is<AuditEntry>(e =>
                e.EventCode == AuditEventCode.UserLogout && e.EntityId == _userId && e.OwnerId == _userId));
            _refreshTokens.RevokeTokenAsync("token", Arg.Any<CancellationToken>());
        });
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogoutAsync_withoutToken_savesTheEntry()
    {
        await CreateSut().LogoutAsync(_userId, null, TestContext.Current.CancellationToken);

        _auditWriter.Received(1).Record(Arg.Is<AuditEntry>(e => e.EventCode == AuditEventCode.UserLogout));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
