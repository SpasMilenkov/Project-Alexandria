using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using Alexandria.Repositories;
using Alexandria.Services.Storage;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace Alexandria.Tests.Integration.Audit;

/// <summary>
/// The parts of a split export are bounded by row positions in Postgres; downloading every part
/// has to give back each row exactly once, even when several rows share a timestamp.
/// </summary>
public class AuditExportPlanTests(DatabaseFixture fixture) : AuditTriggerTestBase(fixture)
{
    private const int PartSize = 3;

    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // Minutes after Start; repeated values are rows written in the same instant.
    private static readonly int[] Minutes = [0, 1, 2, 2, 3, 4, 4, 4];

    private AuditLogFilter Filter => new() { CorrelationId = CorrelationId };

    [Fact]
    public async Task Parts_coverEveryRowOnceInNewestFirstOrder()
    {
        await SeedAsync(Minutes);

        await using var db = Fixture.CreateDbContext();
        var logs = new AuditLogRepository(db);

        var plan = await AuditService.PlanPartsAsync(logs, Filter, PartSize, Ct);

        plan.Total.Should().Be(Minutes.Length);
        plan.Parts.Select(p => p.Rows).Should().Equal(3, 3, 2);
        plan.Parts[^1].EndAt.Should().BeNull();
        plan.Parts[^1].OldestAt.Should().Be(Start);

        var downloaded = new List<Guid>();
        foreach (var part in plan.Parts)
            downloaded.AddRange(await DownloadAsync(logs, part));

        var everyRow = await logs.GetRecentAsync(Filter, 100, ct: Ct);
        downloaded.Should().Equal(everyRow.Select(r => r.Id));
    }

    [Fact]
    public async Task Parts_keepTheirRows_whenNewerRowsArriveAfterPlanning()
    {
        await SeedAsync(Minutes);

        await using var db = Fixture.CreateDbContext();
        var logs = new AuditLogRepository(db);

        var plan = await AuditService.PlanPartsAsync(logs, Filter, PartSize, Ct);
        var before = await DownloadAsync(logs, plan.Parts[0]);

        await SeedAsync([10, 11]);
        var after = await DownloadAsync(logs, plan.Parts[0]);

        after.Should().Equal(before);
    }

    [Fact]
    public async Task Plan_withNoMatchingRows_hasNoParts()
    {
        await using var db = Fixture.CreateDbContext();

        var plan = await AuditService.PlanPartsAsync(new AuditLogRepository(db), Filter, PartSize, Ct);

        plan.Total.Should().Be(0);
        plan.Parts.Should().BeEmpty();
    }

    private async Task<List<Guid>> DownloadAsync(AuditLogRepository logs, AuditExportPart part)
    {
        AuditCursor? end = part.EndAt is { } endAt ? new AuditCursor(endAt, part.EndId!.Value) : null;
        var filter = Filter with { UpTo = new AuditCursor(part.StartAt, part.StartId), After = end };

        var ids = new List<Guid>();
        await foreach (var row in logs.StreamAsync(filter, PartSize, Ct))
            ids.Add(row.Id);

        return ids;
    }

    private async Task SeedAsync(IEnumerable<int> minutes)
    {
        await using var seed = Fixture.CreateDbContext();

        foreach (var minute in minutes)
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
                Timestamp = Start.AddMinutes(minute),
                Source = LogSource.API,
            });
        }

        await seed.SaveChangesAsync(Ct);
    }
}
