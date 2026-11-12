using System.Text.Json;
using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Common.Settings;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using Alexandria.Dto.Files;
using Alexandria.Services.Storage;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.Audit;

public class AuditServiceTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogRepository _logs = Substitute.For<IAuditLogRepository>();

    public AuditServiceTests()
    {
        _unitOfWork.AuditLogs.Returns(_logs);
        _logs.FetchAuditLogAsync(Arg.Any<AuditLogFilter>(), Arg.Any<AuditPageRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedResult<AuditLogResult>());
    }

    private AuditService CreateSut() => new(_unitOfWork, Options.Create(new AuditOptions { ExportMaxRows = 25 }));

    private static AuditLogResult Row(AuditEventCode code, DateTimeOffset at, string? ip = null) => new()
    {
        Id = Guid.NewGuid(),
        OperationType = OperationType.Login,
        EntityType = EntityType.User,
        UserId = Guid.NewGuid(),
        EntityId = Guid.NewGuid(),
        EventCode = code,
        Timestamp = at,
        IpAddress = ip,
        LogSource = LogSource.API,
    };

    [Fact]
    public async Task GetLogsAsync_alwaysScopesToCaller()
    {
        var userId = Guid.NewGuid();
        var query = new AuditLogQuery { Page = 2, PageSize = 10, EntityType = EntityType.File };

        await CreateSut().GetLogsAsync(userId, query, TestContext.Current.CancellationToken);

        await _logs.Received(1).FetchAuditLogAsync(
            Arg.Is<AuditLogFilter>(f => f.ScopeUserId == userId && f.EntityType == EntityType.File),
            Arg.Is<AuditPageRequest>(p => p.Page == 2 && p.PageSize == 10),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetLogsAsync_filteredToOneEntity_includesItsChildrenLikeTheExport()
    {
        var userId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var query = new AuditLogQuery { Page = 1, PageSize = 20, EntityType = EntityType.File, EntityId = fileId };

        await CreateSut().GetLogsAsync(userId, query, TestContext.Current.CancellationToken);

        await _logs.Received(1).FetchAuditLogAsync(
            Arg.Is<AuditLogFilter>(f => f.EntityId == fileId && f.IncludeChildren),
            Arg.Any<AuditPageRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetLogsAsync_withoutAnEntity_leavesChildrenOff()
    {
        await CreateSut().GetLogsAsync(Guid.NewGuid(), new AuditLogQuery { Page = 1, PageSize = 20 },
            TestContext.Current.CancellationToken);

        await _logs.Received(1).FetchAuditLogAsync(
            Arg.Is<AuditLogFilter>(f => !f.IncludeChildren),
            Arg.Any<AuditPageRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetEntityHistoryAsync_nonAdmin_isScopedAndIncludesChildren()
    {
        var viewer = new AuditViewer(Guid.NewGuid(), IsAdmin: false);
        var fileId = Guid.NewGuid();

        await CreateSut().GetEntityHistoryAsync(viewer, EntityType.File, fileId,
            new AuditPageRequest { Page = 1, PageSize = 20 }, TestContext.Current.CancellationToken);

        await _logs.Received(1).FetchAuditLogAsync(
            Arg.Is<AuditLogFilter>(f =>
                f.ScopeUserId == viewer.UserId && f.EntityId == fileId && f.IncludeChildren),
            Arg.Any<AuditPageRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetEntityHistoryAsync_admin_isUnscoped()
    {
        var viewer = new AuditViewer(Guid.NewGuid(), IsAdmin: true);

        await CreateSut().GetEntityHistoryAsync(viewer, EntityType.File, Guid.NewGuid(),
            new AuditPageRequest { Page = 1, PageSize = 20 }, TestContext.Current.CancellationToken);

        await _logs.Received(1).FetchAuditLogAsync(
            Arg.Is<AuditLogFilter>(f => f.ScopeUserId == null),
            Arg.Any<AuditPageRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Scope_nonAdmin_dropsUserAndOwnerFiltersAndScopesToSelf()
    {
        var viewer = new AuditViewer(Guid.NewGuid(), IsAdmin: false);
        var filter = new AuditLogFilter { UserId = Guid.NewGuid(), OwnerId = Guid.NewGuid(), Source = LogSource.System };

        var scoped = AuditService.Scope(viewer, filter);

        scoped.ScopeUserId.Should().Be(viewer.UserId);
        scoped.UserId.Should().BeNull();
        scoped.OwnerId.Should().BeNull();
        scoped.Source.Should().Be(LogSource.System);
    }

    [Fact]
    public void Scope_admin_keepsFiltersAndLimitsToWhatTheAdminMayRead()
    {
        var admin = new AuditViewer(Guid.NewGuid(), IsAdmin: true);
        var filter = new AuditLogFilter { UserId = Guid.NewGuid(), Source = LogSource.System };

        var scoped = AuditService.Scope(admin, filter);

        scoped.ScopeUserId.Should().BeNull();
        scoped.ReaderId.Should().Be(admin.UserId);
        scoped.UserId.Should().Be(filter.UserId);
        scoped.Source.Should().Be(LogSource.System);
    }

    [Fact]
    public void ExportAsync_capsRowsFromOptions()
    {
        var viewer = new AuditViewer(Guid.NewGuid(), IsAdmin: false);

        _ = CreateSut().ExportAsync(viewer, new AuditLogFilter(), TestContext.Current.CancellationToken);

        _logs.Received(1).StreamAsync(
            Arg.Is<AuditLogFilter>(f => f.ScopeUserId == viewer.UserId), 25, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void BuildOverview_daysAcrossYearBoundary_doNotCollide()
    {
        // Same day-of-year in two different years used to land in one bucket.
        var rows = new List<AuditActivityRow>
        {
            new(OperationType.Create, new DateTimeOffset(2025, 3, 1, 10, 0, 0, TimeSpan.Zero)),
            new(OperationType.Update, new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero)),
            new(OperationType.Update, new DateTimeOffset(2026, 3, 1, 23, 30, 0, TimeSpan.Zero)),
            new(OperationType.Delete, new DateTimeOffset(2025, 12, 31, 23, 59, 0, TimeSpan.Zero)),
            new(OperationType.Delete, new DateTimeOffset(2026, 1, 1, 0, 1, 0, TimeSpan.Zero)),
        };

        var overview = AuditService.BuildOverview(rows);

        overview.TotalActivity.Should().Be(5);
        overview.ActivityPerDay.Keys.Should().BeEquivalentTo(
        [
            new DateOnly(2025, 3, 1), new DateOnly(2026, 3, 1), new DateOnly(2025, 12, 31), new DateOnly(2026, 1, 1)
        ]);
        overview.ActivityPerDay[new DateOnly(2026, 3, 1)].TotalOperations.Should().Be(2);
        overview.ActivityPerDay[new DateOnly(2026, 3, 1)].CountPerOperation[OperationType.Update].Should().Be(2);
    }

    [Fact]
    public void BuildOverview_bucketsOnUtcDay()
    {
        // 01:00 at +02:00 is still the previous day in UTC.
        var rows = new List<AuditActivityRow>
        {
            new(OperationType.Create, new DateTimeOffset(2026, 6, 2, 1, 0, 0, TimeSpan.FromHours(2))),
        };

        AuditService.BuildOverview(rows).ActivityPerDay.Keys.Should().Equal(new DateOnly(2026, 6, 1));
    }

    [Fact]
    public void Overview_serializesDayKeysAsIsoDates()
    {
        var overview = AuditService.BuildOverview(
        [
            new AuditActivityRow(OperationType.Create, new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero))
        ]);

        var json = JsonSerializer.Serialize(overview, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.Should().Contain("\"2026-01-05\"");
    }

    [Fact]
    public async Task GetSecuritySummaryAsync_queriesSignInsAndFailuresSeparately()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var login = Row(AuditEventCode.UserLogin, now.AddDays(-1), "10.0.0.1");
        var failure = Row(AuditEventCode.UserLoginFailed, now.AddHours(-1), "6.6.6.6");

        _logs.GetRecentAsync(Arg.Is<AuditLogFilter>(f => f.EventCodes!.Single() == AuditEventCode.UserLogin),
            Arg.Any<int>(), Arg.Any<SortDirection>(), Arg.Any<CancellationToken>()).Returns([login]);
        _logs.GetRecentAsync(Arg.Is<AuditLogFilter>(f => f.EventCodes!.Single() == AuditEventCode.UserLoginFailed),
            Arg.Any<int>(), Arg.Any<SortDirection>(), Arg.Any<CancellationToken>()).Returns([failure]);
        _logs.CountAsync(Arg.Any<AuditLogFilter>(), Arg.Any<CancellationToken>()).Returns(600);
        _logs.GetSignInHistoryAsync(userId, Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns([new AuditSignInHistoryRow(userId, "10.0.0.1", now.AddDays(-3), now.AddDays(-1), 2)]);

        var summary = await CreateSut().GetSecuritySummaryAsync(userId, TestContext.Current.CancellationToken);

        summary.RecentLogins.Should().Equal(login);
        summary.RecentFailedAttempts.Should().Equal(failure);
        summary.FailedAttemptsLast30Days.Should().Be(600);
        summary.KnownIpAddresses.Should().ContainSingle().Which.LoginCount.Should().Be(2);

        await _logs.Received(1).CountAsync(
            Arg.Is<AuditLogFilter>(f => f.OwnerId == userId && f.EventCodes!.Single() == AuditEventCode.UserLoginFailed
                && f.From >= now.AddDays(-30).AddMinutes(-1)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ToKnownIpAddresses_skipsRowsWithoutAnAddressAndPutsTheLatestFirst()
    {
        var now = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
        var account = Guid.NewGuid();

        var known = AuditService.ToKnownIpAddresses(
        [
            new AuditSignInHistoryRow(account, "10.0.0.1", now.AddDays(-9), now.AddDays(-5), 3),
            new AuditSignInHistoryRow(account, null, now.AddDays(-2), now.AddDays(-2), 1),
            new AuditSignInHistoryRow(account, "10.0.0.2", now.AddDays(-4), now.AddDays(-1), 1),
        ]);

        known.Select(ip => ip.IpAddress).Should().Equal("10.0.0.2", "10.0.0.1");
        known[1].LoginCount.Should().Be(3);
        known[1].FirstSeen.Should().Be(now.AddDays(-9));
    }
}
