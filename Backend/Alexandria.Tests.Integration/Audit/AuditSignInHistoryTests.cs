using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Repositories;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace Alexandria.Tests.Integration.Audit;

/// <summary>
/// The access overview leans on Postgres to fold the whole sign-in history into one row per
/// account and address, so the grouping has to translate to SQL rather than run in memory.
/// </summary>
public class AuditSignInHistoryTests(DatabaseFixture fixture) : AuditTriggerTestBase(fixture)
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SignInHistory_foldsEachAccountAndAddressIntoFirstAndLastSeen()
    {
        await using (var seed = Fixture.CreateDbContext())
        {
            seed.AuditLogs.AddRange(
                SignIn("10.0.0.1", Start),
                SignIn("10.0.0.1", Start.AddDays(40)),
                SignIn("10.0.0.1", Start.AddDays(10)),
                SignIn("10.0.0.2", Start.AddDays(5)),
                SignIn(null, Start.AddDays(7)),
                SignIn("10.0.0.3", Start.AddDays(9), AuditEventCode.UserLoginFailed));
            await seed.SaveChangesAsync(Ct);
        }

        await using var db = Fixture.CreateDbContext();
        var history = await new AuditLogRepository(db).GetSignInHistoryAsync(ct: Ct);

        history.Where(h => h.AccountId == UserId)
            .Select(h => (h.IpAddress, h.FirstSeen, h.LastSeen))
            .Should().BeEquivalentTo([
                ("10.0.0.1", Start, Start.AddDays(40)),
                ("10.0.0.2", Start.AddDays(5), Start.AddDays(5)),
                ((string?)null, Start.AddDays(7), Start.AddDays(7)),
            ]);
    }

    [Fact]
    public async Task SignInHistory_narrowedToOneAccountAndStart_countsOnlyThoseSignIns()
    {
        var other = Guid.NewGuid();

        await using (var seed = Fixture.CreateDbContext())
        {
            seed.AuditLogs.AddRange(
                SignIn("10.0.0.1", Start),
                SignIn("10.0.0.1", Start.AddDays(20)),
                SignIn("10.0.0.1", Start.AddDays(30)),
                SignIn("10.0.0.1", Start.AddDays(25), owner: other));
            await seed.SaveChangesAsync(Ct);
        }

        await using var db = Fixture.CreateDbContext();
        var history = await new AuditLogRepository(db).GetSignInHistoryAsync(UserId, Start.AddDays(10), Ct);

        var row = history.Should().ContainSingle().Subject;
        row.AccountId.Should().Be(UserId);
        row.FirstSeen.Should().Be(Start.AddDays(20));
        row.LastSeen.Should().Be(Start.AddDays(30));
        row.SignIns.Should().Be(2);
    }

    private AuditLog SignIn(string? ip, DateTimeOffset at, AuditEventCode code = AuditEventCode.UserLogin,
        Guid? owner = null) => new()
    {
        Id = Guid.NewGuid(),
        OperationType = OperationType.Login,
        EntityType = EntityType.User,
        EventCode = code,
        UserId = UserId,
        OwnerId = owner ?? UserId,
        IpAddress = ip,
        CorrelationId = CorrelationId,
        Timestamp = at,
        Source = LogSource.API,
    };
}
