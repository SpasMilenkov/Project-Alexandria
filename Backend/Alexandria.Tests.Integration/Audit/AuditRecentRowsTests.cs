using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using Alexandria.Repositories;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace Alexandria.Tests.Integration.Audit;

/// <summary>
/// Ascending reads re-sort after the limit in an outer query; this checks Postgres still keeps
/// the newest rows rather than the oldest.
/// </summary>
public class AuditRecentRowsTests(DatabaseFixture fixture) : AuditTriggerTestBase(fixture)
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetRecentAsync_ascending_keepsTheNewestRowsOldestFirst()
    {
        await SeedAsync(5);

        await using var db = Fixture.CreateDbContext();
        var rows = await new AuditLogRepository(db).GetRecentAsync(
            new AuditLogFilter { CorrelationId = CorrelationId }, 3, SortDirection.Asc, Ct);

        rows.Select(r => r.Timestamp).Should().Equal(Start.AddMinutes(2), Start.AddMinutes(3), Start.AddMinutes(4));
    }

    [Fact]
    public async Task GetRecentAsync_byDefault_returnsTheNewestRowsNewestFirst()
    {
        await SeedAsync(5);

        await using var db = Fixture.CreateDbContext();
        var rows = await new AuditLogRepository(db).GetRecentAsync(
            new AuditLogFilter { CorrelationId = CorrelationId }, 3, ct: Ct);

        rows.Select(r => r.Timestamp).Should().Equal(Start.AddMinutes(4), Start.AddMinutes(3), Start.AddMinutes(2));
    }

    [Fact]
    public async Task CountAsync_countsEveryMatchingRow()
    {
        await SeedAsync(5);

        await using var db = Fixture.CreateDbContext();
        var count = await new AuditLogRepository(db).CountAsync(
            new AuditLogFilter { CorrelationId = CorrelationId, From = Start.AddMinutes(1) }, Ct);

        count.Should().Be(4);
    }

    private async Task SeedAsync(int count)
    {
        await using var seed = Fixture.CreateDbContext();

        for (var i = 0; i < count; i++)
        {
            seed.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                OperationType = OperationType.Login,
                EntityType = EntityType.User,
                EventCode = AuditEventCode.UserLogin,
                UserId = UserId,
                OwnerId = UserId,
                CorrelationId = CorrelationId,
                Timestamp = Start.AddMinutes(i),
                Source = LogSource.API,
            });
        }

        await seed.SaveChangesAsync(Ct);
    }
}
