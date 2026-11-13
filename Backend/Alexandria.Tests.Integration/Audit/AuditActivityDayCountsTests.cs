using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using Alexandria.Repositories;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Alexandria.Tests.Integration.Audit;

/// <summary>
/// The activity overview counts per UTC day in Postgres, so the day buckets have to come out of
/// the SQL translation the same way whatever time zone the database session runs in.
/// </summary>
public class AuditActivityDayCountsTests(DatabaseFixture fixture) : AuditTriggerTestBase(fixture)
{
    private static readonly ActivityQuery June = new()
    {
        StartDate = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
        EndDate = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
    };

    [Fact]
    public async Task DayCounts_groupEachOperationPerUtcDay()
    {
        await using (var seed = Fixture.CreateDbContext())
        {
            seed.AuditLogs.AddRange(
                Entry(OperationType.Create, new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero)),
                Entry(OperationType.Create, new DateTimeOffset(2026, 6, 1, 20, 0, 0, TimeSpan.Zero)),
                Entry(OperationType.Read, new DateTimeOffset(2026, 6, 1, 21, 0, 0, TimeSpan.Zero)),
                Entry(OperationType.Delete, new DateTimeOffset(2026, 6, 3, 12, 0, 0, TimeSpan.Zero)));
            await seed.SaveChangesAsync(Ct);
        }

        await using var db = Fixture.CreateDbContext();
        var counts = await new AuditLogRepository(db).GetActivityDayCountsAsync(UserId, June, Ct);

        counts.Should().BeEquivalentTo([
            new AuditActivityDayCount(new DateOnly(2026, 6, 1), OperationType.Create, 2),
            new AuditActivityDayCount(new DateOnly(2026, 6, 1), OperationType.Read, 1),
            new AuditActivityDayCount(new DateOnly(2026, 6, 3), OperationType.Delete, 1),
        ]);
    }

    [Fact]
    public async Task DayCounts_bucketOnUtcDay_whateverTheSessionTimeZone()
    {
        await using (var seed = Fixture.CreateDbContext())
        {
            seed.AuditLogs.Add(Entry(OperationType.Update, new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero)));
            await seed.SaveChangesAsync(Ct);
        }

        await using var db = Fixture.CreateDbContext();
        await db.Database.OpenConnectionAsync(Ct);

        // At +14:00 that row is already June 2, so a truncation that followed the session would move it.
        await db.Database.ExecuteSqlRawAsync("SET TIME ZONE 'Pacific/Kiritimati'", Ct);

        var counts = await new AuditLogRepository(db).GetActivityDayCountsAsync(UserId, June, Ct);

        counts.Should().ContainSingle().Which.Day.Should().Be(new DateOnly(2026, 6, 1));
    }

    [Fact]
    public async Task DayCounts_skipSignInsOtherAccountsAndRowsOutsideTheRange()
    {
        var other = Guid.NewGuid();
        var inRange = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

        await using (var seed = Fixture.CreateDbContext())
        {
            seed.AuditLogs.AddRange(
                Entry(OperationType.Create, inRange),
                Entry(OperationType.Login, inRange),
                Entry(OperationType.Logout, inRange),
                Entry(OperationType.Create, inRange, other),
                Entry(OperationType.Create, new DateTimeOffset(2026, 5, 31, 23, 0, 0, TimeSpan.Zero)));
            await seed.SaveChangesAsync(Ct);
        }

        await using var db = Fixture.CreateDbContext();
        var counts = await new AuditLogRepository(db).GetActivityDayCountsAsync(UserId, June, Ct);

        counts.Should().BeEquivalentTo([new AuditActivityDayCount(new DateOnly(2026, 6, 10), OperationType.Create, 1)]);
    }

    private AuditLog Entry(OperationType operation, DateTimeOffset at, Guid? account = null) => new()
    {
        Id = Guid.NewGuid(),
        OperationType = operation,
        EntityType = EntityType.File,
        EventCode = AuditEventCode.FileCreated,
        UserId = account ?? UserId,
        OwnerId = account ?? UserId,
        CorrelationId = CorrelationId,
        Timestamp = at,
        Source = LogSource.API,
    };
}
