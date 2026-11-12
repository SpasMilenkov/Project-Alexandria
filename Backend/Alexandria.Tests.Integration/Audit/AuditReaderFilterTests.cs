using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using Alexandria.Repositories;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace Alexandria.Tests.Integration.Audit;

/// <summary>
/// The reader rule has to run in SQL so paging and totals never count rows the reader may not
/// see; this checks Postgres gets the enum lists right.
/// </summary>
public class AuditReaderFilterTests(DatabaseFixture fixture) : AuditTriggerTestBase(fixture)
{
    private readonly Guid _admin = Guid.NewGuid();

    [Fact]
    public async Task ReaderFilter_leavesOutOtherAccountsLibrary_inSql()
    {
        await using (var seed = Fixture.CreateDbContext())
        {
            seed.AuditLogs.AddRange(
                Row("alice file", EntityType.File, AuditEventCode.FileSoftDeleted, UserId),
                Row("alice theme", EntityType.Setting, AuditEventCode.SettingChanged, UserId),
                Row("alice sign-in", EntityType.User, AuditEventCode.UserLogin, UserId),
                Row("system setting", EntityType.Setting, AuditEventCode.AdminSettingChanged, null));
            await seed.SaveChangesAsync(Ct);
        }

        await using var db = Fixture.CreateDbContext();
        var result = await new AuditLogRepository(db).FetchAuditLogAsync(
            new AuditLogFilter { ReaderId = _admin, CorrelationId = CorrelationId },
            new AuditPageRequest { Page = 1, PageSize = 20 }, Ct);

        result.Items.Select(r => r.EntityName).Should().BeEquivalentTo(["alice sign-in", "system setting"]);
        result.TotalCount.Should().Be(2);
    }

    private AuditLog Row(string name, EntityType type, AuditEventCode code, Guid? owner) => new()
    {
        Id = Guid.NewGuid(),
        OperationType = OperationType.Update,
        EntityType = type,
        EventCode = code,
        UserId = UserId,
        OwnerId = owner,
        EntityName = name,
        CorrelationId = CorrelationId,
        Timestamp = DateTimeOffset.UtcNow,
        Source = LogSource.API,
    };
}
