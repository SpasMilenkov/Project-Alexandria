using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Common.Settings;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using Alexandria.Dto.Files;
using Alexandria.Repositories;
using Alexandria.Services.Storage;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.Audit;

public class AuditPrivacyTests
{
    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly Guid Alice = Guid.NewGuid();
    private static readonly AuditViewer AdminViewer = new(Admin, IsAdmin: true);

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogRepository _logs = Substitute.For<IAuditLogRepository>();

    public AuditPrivacyTests()
    {
        _unitOfWork.AuditLogs.Returns(_logs);
    }

    private AuditService CreateSut() => new(_unitOfWork, Options.Create(new AuditOptions { ExportMaxRows = 25 }));

    private static AuditLog Row(string name, EntityType type, AuditEventCode code, Guid actor, Guid? owner) => new()
    {
        Id = Guid.NewGuid(),
        OperationType = OperationType.Update,
        EntityType = type,
        EventCode = code,
        UserId = actor,
        OwnerId = owner,
        EntityName = name,
        Timestamp = DateTimeOffset.UtcNow,
    };

    private static List<string?> VisibleTo(Guid reader, params AuditLog[] rows) =>
        AuditLogRepository.ApplyFilter(rows.AsQueryable(), new AuditLogFilter { ReaderId = reader })
            .Select(r => r.EntityName)
            .ToList();

    [Fact]
    public void AnotherAccountsLibrary_neverReachesTheReader()
    {
        var visible = VisibleTo(Admin,
            Row("moved", EntityType.File, AuditEventCode.FilesMovedBulk, Alice, Alice),
            Row("folder", EntityType.Directory, AuditEventCode.DirectorySoftDeleted, Alice, Alice),
            Row("playlist", EntityType.Playlist, AuditEventCode.PlaylistSoftDeleted, Alice, Alice),
            Row("theme", EntityType.Setting, AuditEventCode.SettingChanged, Alice, Alice));

        visible.Should().BeEmpty();
    }

    [Fact]
    public void AccountSecurityAndSystemRows_reachAdmins()
    {
        var visible = VisibleTo(Admin,
            Row("sign-in", EntityType.User, AuditEventCode.UserLogin, Alice, Alice),
            Row("failed", EntityType.User, AuditEventCode.UserLoginFailed, Alice, Alice),
            Row("system setting", EntityType.Setting, AuditEventCode.AdminSettingChanged, Alice, null),
            Row("system tag", EntityType.Tag, AuditEventCode.TagCreated, Alice, null));

        visible.Should().BeEquivalentTo(["sign-in", "failed", "system setting", "system tag"]);
    }

    [Fact]
    public void OwnerlessLibraryRows_neverReachTheReader()
    {
        var visible = VisibleTo(Admin,
            Row("orphaned version", EntityType.FileVersion, AuditEventCode.FileVersionDeletedPermanently, Alice, null),
            Row("orphaned file tag", EntityType.FileTag, AuditEventCode.FileTagRemoved, Alice, null),
            Row("orphaned theme", EntityType.Setting, AuditEventCode.SettingChanged, Alice, null));

        visible.Should().BeEmpty();
    }

    [Fact]
    public void RowsTheReaderMadeOrOwns_reachThem()
    {
        var visible = VisibleTo(Admin,
            Row("admin acted", EntityType.File, AuditEventCode.FilesMovedBulk, Admin, Alice),
            Row("on admin's file", EntityType.File, AuditEventCode.FileSoftDeleted, Alice, Admin));

        visible.Should().BeEquivalentTo(["admin acted", "on admin's file"]);
    }

    [Fact]
    public void ExcludePersonalContent_dropsTheReadersOwnLibraryButKeepsAccountRows()
    {
        var rows = new[]
        {
            Row("admin's own file", EntityType.File, AuditEventCode.FileSoftDeleted, Admin, Admin),
            Row("admin's own theme", EntityType.Setting, AuditEventCode.SettingChanged, Admin, Admin),
            Row("admin sign-in", EntityType.User, AuditEventCode.UserLogin, Admin, Admin),
            Row("system setting", EntityType.Setting, AuditEventCode.AdminSettingChanged, Admin, null),
        };

        var visible = AuditLogRepository.ApplyFilter(rows.AsQueryable(),
                new AuditLogFilter { ReaderId = Admin, ExcludePersonalContent = true })
            .Select(r => r.EntityName)
            .ToList();

        visible.Should().BeEquivalentTo(["admin sign-in", "system setting"]);
    }

    [Fact]
    public async Task SearchAsync_admin_leavesOutEveryLibrary()
    {
        await CreateSut().SearchAsync(AdminViewer, new AuditLogFilter(),
            new AuditPageRequest { Page = 1, PageSize = 20 }, TestContext.Current.CancellationToken);

        await _logs.Received(1).FetchAuditLogAsync(Arg.Is<AuditLogFilter>(f => f.ExcludePersonalContent),
            Arg.Any<AuditPageRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ExportAsync_ownHistory_keepsTheViewersLibrary()
    {
        _ = CreateSut().ExportAsync(AdminViewer with { IsAdmin = false }, new AuditLogFilter(),
            TestContext.Current.CancellationToken);

        _logs.Received(1).StreamAsync(Arg.Is<AuditLogFilter>(f => !f.ExcludePersonalContent && f.ScopeUserId == Admin),
            Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_admin_keepsAccountFiltersButAlwaysSetsTheReader()
    {
        var filter = new AuditLogFilter { UserId = Alice, OwnerId = Alice };

        await CreateSut().SearchAsync(AdminViewer, filter, new AuditPageRequest { Page = 1, PageSize = 20 },
            TestContext.Current.CancellationToken);

        await _logs.Received(1).FetchAuditLogAsync(
            Arg.Is<AuditLogFilter>(f => f.ReaderId == Admin && f.ScopeUserId == null && f.UserId == Alice
                && f.OwnerId == Alice),
            Arg.Any<AuditPageRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetEntityHistoryAsync_setsTheReader()
    {
        await CreateSut().GetEntityHistoryAsync(AdminViewer, EntityType.File, Guid.NewGuid(),
            new AuditPageRequest { Page = 1, PageSize = 20 }, TestContext.Current.CancellationToken);

        await _logs.Received(1).FetchAuditLogAsync(Arg.Is<AuditLogFilter>(f => f.ReaderId == Admin),
            Arg.Any<AuditPageRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetCorrelatedAsync_setsTheReader()
    {
        _logs.GetRecentAsync(Arg.Any<AuditLogFilter>(), Arg.Any<int>(), Arg.Any<SortDirection>(),
            Arg.Any<CancellationToken>()).Returns([]);

        await CreateSut().GetCorrelatedAsync(AdminViewer, Guid.NewGuid(), TestContext.Current.CancellationToken);

        await _logs.Received(1).GetRecentAsync(Arg.Is<AuditLogFilter>(f => f.ReaderId == Admin), Arg.Any<int>(),
            SortDirection.Asc, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ExportAsync_setsTheReader()
    {
        _ = CreateSut().ExportAsync(AdminViewer, new AuditLogFilter(), TestContext.Current.CancellationToken);

        _logs.Received(1).StreamAsync(Arg.Is<AuditLogFilter>(f => f.ReaderId == Admin), Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetLogsAsync_setsTheReader()
    {
        await CreateSut().GetLogsAsync(Alice, new AuditLogQuery { Page = 1, PageSize = 20 },
            TestContext.Current.CancellationToken);

        await _logs.Received(1).FetchAuditLogAsync(Arg.Is<AuditLogFilter>(f => f.ReaderId == Alice),
            Arg.Any<AuditPageRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlanExportAsync_nonAdmin_isScopedLikeTheExport()
    {
        _logs.GetPositionsAsync(Arg.Any<AuditLogFilter>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<SortDirection>(), Arg.Any<CancellationToken>()).Returns([]);

        var plan = await CreateSut().PlanExportAsync(AdminViewer with { IsAdmin = false },
            new AuditLogFilter { UserId = Alice, OwnerId = Alice }, TestContext.Current.CancellationToken);

        plan.Parts.Should().BeEmpty();
        plan.PartSize.Should().Be(25);
        await _logs.Received(1).GetPositionsAsync(
            Arg.Is<AuditLogFilter>(f => f.ScopeUserId == Admin && f.ReaderId == Admin && f.UserId == null
                && f.OwnerId == null),
            0, 1, SortDirection.Desc, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Scope_keepsPartBoundsButNeverWidensTheViewer()
    {
        var upTo = new AuditCursor(DateTimeOffset.UtcNow, Guid.NewGuid());
        var after = new AuditCursor(DateTimeOffset.UtcNow.AddDays(-1), Guid.NewGuid());

        var scoped = AuditService.Scope(AdminViewer with { IsAdmin = false },
            new AuditLogFilter { UserId = Alice, UpTo = upTo, After = after });

        scoped.UpTo.Should().Be(upTo);
        scoped.After.Should().Be(after);
        scoped.ScopeUserId.Should().Be(Admin);
        scoped.UserId.Should().BeNull();
    }
}
