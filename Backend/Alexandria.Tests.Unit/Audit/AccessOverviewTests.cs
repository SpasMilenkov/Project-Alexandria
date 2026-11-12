using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using Alexandria.Dto.Users;
using Alexandria.Services.Storage.Access;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Alexandria.Tests.Unit.Audit;

public class AccessOverviewTests
{
    private const int Window = 30;

    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset InWindow = Now.AddDays(-3);
    private static readonly DateTimeOffset PreviousWindow = Now.AddDays(-45);
    private static readonly DateTimeOffset LongAgo = Now.AddDays(-200);

    private static readonly Guid Alice = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogRepository _logs = Substitute.For<IAuditLogRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _tokens = Substitute.For<IRefreshTokenRepository>();

    public AccessOverviewTests()
    {
        _unitOfWork.AuditLogs.Returns(_logs);
        _unitOfWork.Users.Returns(_users);
        _unitOfWork.RefreshTokens.Returns(_tokens);
    }

    private AccessOverviewService CreateSut() => new(_unitOfWork, NullLogger<AccessOverviewService>.Instance);

    private static AccountStateRow Account(Guid id, string name, DateTimeOffset? lockoutEnd = null) =>
        new(id, name, $"{name.ToLowerInvariant()}@example.test", lockoutEnd);

    private static AuditSignInRow SignIn(Guid? account, string ip, DateTimeOffset at) => new(account, ip, null, at);

    private static AuditSignInHistoryRow Seen(Guid? account, string ip, DateTimeOffset first, DateTimeOffset? last = null) =>
        new(account, ip, first, last ?? first, 1);

    private static FailedSignIn Failure(Guid? account, string ip, DateTimeOffset at,
        LoginFailureReason? reason = LoginFailureReason.BadPassword, string? email = null) =>
        new(account, ip, reason, email, at);

    private static AccessOverviewInput Input(
        IReadOnlyList<AccountStateRow>? accounts = null,
        IReadOnlyList<AccountSessionRow>? sessions = null,
        IReadOnlyList<AuditSignInRow>? signIns = null,
        IReadOnlyList<AuditSignInHistoryRow>? history = null,
        IReadOnlyList<FailedSignIn>? failures = null) => new()
    {
        Accounts = accounts ?? [Account(Alice, "Alice"), Account(Bob, "Bob")],
        Sessions = sessions ?? [],
        SignIns = signIns ?? [],
        History = history ?? [],
        Failures = failures ?? [],
    };

    [Fact]
    public void Build_signInsOutsideTheWindow_countOnlyTowardsLastSignIn()
    {
        var overview = AccessOverviewService.Build(Input(
            signIns: [SignIn(Alice, "10.0.0.1", InWindow), SignIn(Alice, "10.0.0.1", InWindow.AddHours(1))],
            history: [Seen(Alice, "10.0.0.1", InWindow, InWindow.AddHours(1)), Seen(Bob, "10.0.0.2", LongAgo)]),
            Now, Window);

        overview.ActiveAccounts.Should().Be(1);
        overview.TotalAccounts.Should().Be(2);

        var bob = overview.Accounts.Single(a => a.UserId == Bob);
        bob.SignIns.Should().Be(0);
        bob.LastSignInAt.Should().Be(LongAgo);

        overview.Accounts.Single(a => a.UserId == Alice).SignIns.Should().Be(2);
    }

    [Fact]
    public void Build_accounts_recentSignInFirstAndNeverSignedInLast()
    {
        var carol = Guid.NewGuid();

        var overview = AccessOverviewService.Build(Input(
            accounts: [Account(carol, "Carol"), Account(Bob, "Bob"), Account(Alice, "Alice")],
            signIns: [SignIn(Alice, "10.0.0.1", InWindow)],
            history: [Seen(Bob, "10.0.0.2", LongAgo), Seen(Alice, "10.0.0.1", InWindow)]), Now, Window);

        overview.Accounts.Select(a => a.Name).Should().Equal("Alice", "Bob", "Carol");
    }

    [Fact]
    public void Build_newNetwork_onlyWhenTheFirstSignInFromThatAddressIsInsideTheWindow()
    {
        var overview = AccessOverviewService.Build(Input(
            signIns: [SignIn(Alice, "10.0.0.1", InWindow), SignIn(Alice, "10.0.0.9", InWindow)],
            history: [Seen(Alice, "10.0.0.1", LongAgo, InWindow), Seen(Alice, "10.0.0.9", InWindow)]), Now, Window);

        overview.Accounts.Single(a => a.UserId == Alice).NewNetworks.Should().Be(1);
    }

    [Fact]
    public void Build_failedSignIns_compareWithTheWindowJustBefore()
    {
        var overview = AccessOverviewService.Build(Input(failures:
        [
            Failure(Alice, "10.0.0.5", InWindow),
            Failure(Alice, "10.0.0.5", InWindow),
            Failure(Bob, "10.0.0.5", PreviousWindow),
        ]), Now, Window);

        overview.FailedSignIns.Should().Be(2);
        overview.FailedSignInsPrevious.Should().Be(1);
        overview.Accounts.Single(a => a.UserId == Alice).FailedSignIns.Should().Be(2);
        overview.Accounts.Single(a => a.UserId == Bob).FailedSignIns.Should().Be(0);
    }

    [Fact]
    public void Build_failedSources_rankByAttemptsAndCountKnownAccounts()
    {
        var overview = AccessOverviewService.Build(Input(failures:
        [
            Failure(Alice, "10.0.0.5", InWindow),
            Failure(Bob, "10.0.0.5", InWindow),
            Failure(null, "10.0.0.5", InWindow, LoginFailureReason.UnknownUser, "x@example.test"),
            Failure(Alice, "10.0.0.6", InWindow),
        ]), Now, Window);

        overview.FailedSources.Select(s => s.IpAddress).Should().Equal("10.0.0.5", "10.0.0.6");

        var top = overview.FailedSources[0];
        top.Count.Should().Be(3);
        top.AccountCount.Should().Be(2);
    }

    [Fact]
    public void Build_failedByReason_skipsRowsWithoutAReason()
    {
        var overview = AccessOverviewService.Build(Input(failures:
        [
            Failure(Alice, "10.0.0.5", InWindow),
            Failure(Alice, "10.0.0.5", InWindow),
            Failure(Alice, "10.0.0.5", InWindow, LoginFailureReason.LockedOut),
            Failure(Alice, "10.0.0.5", InWindow, null),
        ]), Now, Window);

        overview.FailedByReason.Select(r => (r.Reason, r.Count)).Should().Equal(
            (LoginFailureReason.BadPassword, 2), (LoginFailureReason.LockedOut, 1));
        overview.FailedSignIns.Should().Be(4);
    }

    [Fact]
    public void Build_unknownAccountAttempts_groupEmailsIgnoringCase()
    {
        var overview = AccessOverviewService.Build(Input(failures:
        [
            Failure(null, "10.0.0.5", InWindow, LoginFailureReason.UnknownUser, "Admin@Example.test"),
            Failure(null, "10.0.0.5", InWindow.AddHours(1), LoginFailureReason.UnknownUser, " admin@example.test"),
            Failure(Alice, "10.0.0.5", InWindow, email: "alice@example.test"),
        ]), Now, Window);

        var attempt = overview.UnknownAccountAttempts.Should().ContainSingle().Subject;
        attempt.Email.Should().Be("admin@example.test");
        attempt.Count.Should().Be(2);
        attempt.LastSeen.Should().Be(InWindow.AddHours(1));
    }

    [Fact]
    public void Build_networks_takeFirstSeenFromTheWholeHistory()
    {
        var overview = AccessOverviewService.Build(Input(
            signIns: [SignIn(Alice, "10.0.0.1", InWindow), SignIn(Bob, "10.0.0.1", InWindow.AddHours(2))],
            history:
            [
                Seen(Alice, "10.0.0.1", LongAgo, InWindow),
                Seen(Bob, "10.0.0.1", InWindow.AddHours(2)),
                Seen(Bob, "10.0.0.7", LongAgo),
            ]), Now, Window);

        var network = overview.Networks.Should().ContainSingle().Subject;
        network.IpAddress.Should().Be("10.0.0.1");
        network.FirstSeen.Should().Be(LongAgo);
        network.LastSeen.Should().Be(InWindow.AddHours(2));
        network.AccountCount.Should().Be(2);
        network.SignIns.Should().Be(2);
    }

    [Fact]
    public void Build_sessions_countLiveAccountsOnlyAndListEachBrowserOnce()
    {
        var deleted = Guid.NewGuid();

        var overview = AccessOverviewService.Build(Input(sessions:
        [
            new AccountSessionRow(Alice, "Firefox"),
            new AccountSessionRow(Alice, "Firefox"),
            new AccountSessionRow(Alice, null),
            new AccountSessionRow(deleted, "Chrome"),
        ]), Now, Window);

        overview.LiveSessions.Should().Be(3);

        var alice = overview.Accounts.Single(a => a.UserId == Alice);
        alice.LiveSessions.Should().Be(3);
        alice.SessionUserAgents.Should().Equal("Firefox");
    }

    [Fact]
    public void Build_lockedAccounts_countOnlyLockoutsThatHaveNotEnded()
    {
        var overview = AccessOverviewService.Build(Input(accounts:
        [
            Account(Alice, "Alice", Now.AddHours(1)),
            Account(Bob, "Bob", Now.AddHours(-1)),
        ]), Now, Window);

        overview.LockedAccounts.Should().Be(1);
    }

    [Fact]
    public async Task GetOverviewAsync_readsReasonAndEmailFromMetadata_andKeepsUnreadableRows()
    {
        var at = DateTimeOffset.UtcNow.AddHours(-1);

        _users.GetAccountStatesAsync(Arg.Any<CancellationToken>()).Returns([Account(Alice, "Alice")]);
        _tokens.GetAllActiveAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns([]);
        _logs.GetSignInRowsAsync(AuditEventCode.UserLogin, Arg.Any<DateTimeOffset>(), false,
            Arg.Any<CancellationToken>()).Returns([]);
        _logs.GetSignInHistoryAsync(Arg.Any<Guid?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>()).Returns([]);
        _logs.GetSignInRowsAsync(AuditEventCode.UserLoginFailed, Arg.Any<DateTimeOffset>(), true,
                Arg.Any<CancellationToken>())
            .Returns([
                new AuditSignInRow(null, "10.0.0.5", """{"reason":"unknown_user","email":"ghost@example.test"}""", at),
                new AuditSignInRow(Alice, "10.0.0.5", "{not json", at),
            ]);

        var overview = await CreateSut().GetOverviewAsync(Window, TestContext.Current.CancellationToken);

        overview.FailedSignIns.Should().Be(2);
        overview.FailedByReason.Should().ContainSingle()
            .Which.Reason.Should().Be(LoginFailureReason.UnknownUser);
        overview.UnknownAccountAttempts.Should().ContainSingle()
            .Which.Email.Should().Be("ghost@example.test");
    }

    [Fact]
    public async Task GetOverviewAsync_readsSignInsOnlyFromTheStartOfTheWindow()
    {
        _users.GetAccountStatesAsync(Arg.Any<CancellationToken>()).Returns([Account(Alice, "Alice")]);
        _tokens.GetAllActiveAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns([]);
        _logs.GetSignInHistoryAsync(Arg.Any<Guid?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>()).Returns([]);
        _logs.GetSignInRowsAsync(Arg.Any<AuditEventCode>(), Arg.Any<DateTimeOffset>(), Arg.Any<bool>(),
            Arg.Any<CancellationToken>()).Returns([]);

        var before = DateTimeOffset.UtcNow;
        await CreateSut().GetOverviewAsync(60, TestContext.Current.CancellationToken);
        var after = DateTimeOffset.UtcNow;

        await _logs.Received(1).GetSignInRowsAsync(AuditEventCode.UserLogin,
            Arg.Is<DateTimeOffset>(from => from >= before.AddDays(-60) && from <= after.AddDays(-60)), false,
            Arg.Any<CancellationToken>());
        await _logs.Received(1).GetSignInRowsAsync(AuditEventCode.UserLoginFailed,
            Arg.Is<DateTimeOffset>(from => from >= before.AddDays(-120) && from <= after.AddDays(-120)), true,
            Arg.Any<CancellationToken>());
    }
}
