using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Infrastructure;
using Alexandria.Infrastructure.DocumentWorker;
using Alexandria.Infrastructure.Messaging.Outbox;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fakes;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Alexandria.Tests.Integration.Messaging;

/// <summary>
/// The outbox only holds if the row commits exactly when the change it announces does, which
/// depends on real Postgres transactions, so these run against the migrated schema.
/// </summary>
[Collection("Database")]
public class OutboxPersistenceTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly RecordingOutboxSignal _signal = new();
    private readonly Guid _userId = Guid.NewGuid();
    private ServiceProvider _provider = null!;

    public async ValueTask InitializeAsync()
    {
        await using (var db = fixture.CreateDbContext())
        {
            db.Users.Add(new UserBuilder().WithId(_userId).Build());
            await db.SaveChangesAsync(Ct);
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AlexandriaPostgres"] = fixture.ConnectionString,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddWorkerDatabase(config);
        services.AddServices();

        _provider = services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await fixture.ResetAsync();
    }

    private OutboxWriter Writer(IUnitOfWork unitOfWork) =>
        new(unitOfWork, new FakeMessageBrokerStatus(), _signal, NullLogger<OutboxWriter>.Instance);

    private async Task<Job> SeedJobAsync(JobStatus status)
    {
        var job = new JobBuilder().WithStatus(status).WithUser(_userId).Build();

        await using var db = fixture.CreateDbContext();
        db.Jobs.Add(job);
        await db.SaveChangesAsync(Ct);

        return job;
    }

    private async Task<List<OutboxMessage>> StoredMessagesAsync()
    {
        await using var db = fixture.CreateDbContext();
        return await db.OutboxMessages.AsNoTracking().ToListAsync(Ct);
    }

    [Fact]
    public async Task committed_enqueue_is_stored_and_wakes_the_relay()
    {
        await using var scope = _provider.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await unitOfWork.InTransactionAsync(
            () => Writer(unitOfWork).EnqueueAsync("lyrics.job", [1, 2], Ct), Ct);

        var stored = await StoredMessagesAsync();
        stored.Should().ContainSingle();
        stored[0].RoutingKey.Should().Be("lyrics.job");
        stored[0].Body.Should().Equal(1, 2);
        _signal.Notifications.Should().Be(1);
    }

    [Fact]
    public async Task rolled_back_enqueue_leaves_no_row_and_no_wake_up()
    {
        await using var scope = _provider.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var act = () => unitOfWork.InTransactionAsync(async () =>
        {
            await Writer(unitOfWork).EnqueueAsync("lyrics.job", [1], Ct);
            await unitOfWork.SaveChangesAsync(Ct);
            throw new InvalidOperationException("the change failed after the row was flushed");
        }, Ct);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await StoredMessagesAsync()).Should().BeEmpty();
        _signal.Notifications.Should().Be(0);
    }

    [Fact]
    public async Task execute_update_and_enqueue_commit_or_roll_back_together()
    {
        var committed = await SeedJobAsync(JobStatus.Failed);
        var rolledBack = await SeedJobAsync(JobStatus.Failed);

        await using (var scope = _provider.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await unitOfWork.InTransactionAsync(async () =>
            {
                await unitOfWork.Jobs.UpdateStatusAsync(committed.Id, JobStatus.Queued, ct: Ct);
                await Writer(unitOfWork).EnqueueAsync("transpilation.job", [1], Ct);
            }, Ct);
        }

        await using (var scope = _provider.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var act = () => unitOfWork.InTransactionAsync(async () =>
            {
                await unitOfWork.Jobs.UpdateStatusAsync(rolledBack.Id, JobStatus.Queued, ct: Ct);
                await Writer(unitOfWork).EnqueueAsync("transpilation.job", [2], Ct);
                throw new InvalidOperationException("injected failure");
            }, Ct);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        await using var db = fixture.CreateDbContext();
        (await db.Jobs.SingleAsync(j => j.Id == committed.Id, Ct)).Status.Should().Be(JobStatus.Queued);
        (await db.Jobs.SingleAsync(j => j.Id == rolledBack.Id, Ct)).Status.Should().Be(JobStatus.Failed);
        (await db.OutboxMessages.Select(m => m.Body).ToListAsync(Ct)).Should().ContainSingle()
            .Which.Should().Equal(1);
    }

    [Fact]
    public async Task concurrent_claims_never_take_the_same_row()
    {
        var now = DateTime.UtcNow;
        await using (var db = fixture.CreateDbContext())
        {
            for (var i = 0; i < 4; i++)
            {
                db.OutboxMessages.Add(new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    RoutingKey = $"test.{i}",
                    Body = [(byte)i],
                    AvailableAt = now.AddSeconds(-10 + i),
                    CreatedAt = now
                });
            }

            await db.SaveChangesAsync(Ct);
        }

        await using var firstScope = _provider.CreateAsyncScope();
        await using var secondScope = _provider.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var second = secondScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await first.BeginTransactionAsync(Ct);
        await second.BeginTransactionAsync(Ct);

        var firstClaim = await first.OutboxMessages.ClaimDueAsync(DateTime.UtcNow, 2, Ct);
        var secondClaim = await second.OutboxMessages.ClaimDueAsync(DateTime.UtcNow, 10, Ct);

        await first.RollbackAsync(Ct);
        await second.RollbackAsync(Ct);

        firstClaim.Select(m => m.RoutingKey).Should().Equal("test.0", "test.1");
        secondClaim.Select(m => m.RoutingKey).Should().Equal("test.2", "test.3");
    }

    [Fact]
    public async Task claim_skips_sent_dead_and_not_yet_due_rows()
    {
        var now = DateTime.UtcNow;
        await using (var db = fixture.CreateDbContext())
        {
            db.OutboxMessages.AddRange(
                new OutboxMessage { Id = Guid.NewGuid(), RoutingKey = "due", AvailableAt = now.AddMinutes(-1), CreatedAt = now },
                new OutboxMessage { Id = Guid.NewGuid(), RoutingKey = "sent", AvailableAt = now.AddMinutes(-1), SentAt = now, CreatedAt = now },
                new OutboxMessage { Id = Guid.NewGuid(), RoutingKey = "dead", AvailableAt = now.AddMinutes(-1), DeadAt = now, CreatedAt = now },
                new OutboxMessage { Id = Guid.NewGuid(), RoutingKey = "later", AvailableAt = now.AddMinutes(5), CreatedAt = now });
            await db.SaveChangesAsync(Ct);
        }

        await using var scope = _provider.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await unitOfWork.BeginTransactionAsync(Ct);
        var claimed = await unitOfWork.OutboxMessages.ClaimDueAsync(now, 10, Ct);
        await unitOfWork.RollbackAsync(Ct);

        claimed.Select(m => m.RoutingKey).Should().Equal("due");
    }

    [Fact]
    public async Task resend_dead_makes_them_due_again_and_prune_removes_old_sent_rows()
    {
        var now = DateTime.UtcNow;
        await using (var db = fixture.CreateDbContext())
        {
            db.OutboxMessages.AddRange(
                new OutboxMessage
                {
                    Id = Guid.NewGuid(), RoutingKey = "dead", Attempts = 10, AvailableAt = now.AddHours(-1),
                    DeadAt = now.AddMinutes(-30), LastError = "unroutable", CreatedAt = now.AddHours(-1)
                },
                new OutboxMessage
                {
                    Id = Guid.NewGuid(), RoutingKey = "old-sent", AvailableAt = now.AddDays(-9),
                    SentAt = now.AddDays(-8), CreatedAt = now.AddDays(-9)
                },
                new OutboxMessage
                {
                    Id = Guid.NewGuid(), RoutingKey = "recent-sent", AvailableAt = now.AddDays(-1),
                    SentAt = now.AddDays(-1), CreatedAt = now.AddDays(-1)
                });
            await db.SaveChangesAsync(Ct);
        }

        await using var scope = _provider.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var summaryBefore = await unitOfWork.OutboxMessages.GetSummaryAsync(20, Ct);
        var resent = await unitOfWork.OutboxMessages.ResendDeadAsync(now, Ct);
        var pruned = await unitOfWork.OutboxMessages.DeleteSentBeforeAsync(now.AddDays(-7), Ct);
        var summaryAfter = await unitOfWork.OutboxMessages.GetSummaryAsync(20, Ct);

        summaryBefore.DeadCount.Should().Be(1);
        summaryBefore.PendingCount.Should().Be(0);
        summaryBefore.RecentDead.Should().ContainSingle().Which.LastError.Should().Be("unroutable");
        resent.Should().Be(1);
        pruned.Should().Be(1);
        summaryAfter.DeadCount.Should().Be(0);
        summaryAfter.PendingCount.Should().Be(1);
        summaryAfter.OldestPendingAt.Should().BeCloseTo(now, TimeSpan.FromMilliseconds(1));

        var stored = await StoredMessagesAsync();
        stored.Select(m => m.RoutingKey).Should().BeEquivalentTo("dead", "recent-sent");
        stored.Single(m => m.RoutingKey == "dead").Attempts.Should().Be(0);
        stored.Single(m => m.RoutingKey == "dead").CreatedAt.Should().BeCloseTo(now, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task manual_requeue_accepts_a_job_stuck_in_queued_but_not_a_fresh_one()
    {
        var stuck = new JobBuilder().WithStatus(JobStatus.Queued).WithUser(_userId).Build();
        stuck.CreatedAt = DateTime.UtcNow.AddHours(-2);
        var fresh = await SeedJobAsync(JobStatus.Queued);

        await using (var db = fixture.CreateDbContext())
        {
            db.Jobs.Add(stuck);
            await db.SaveChangesAsync(Ct);
        }

        await using var scope = _provider.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var stalledBefore = DateTime.UtcNow.AddMinutes(-30);

        (await unitOfWork.Jobs.TryResetForManualRequeueAsync(stuck.Id, stalledBefore, Ct)).Should().BeTrue();
        (await unitOfWork.Jobs.TryResetForManualRequeueAsync(fresh.Id, stalledBefore, Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task fail_if_queued_leaves_a_claimed_job_alone()
    {
        var queued = await SeedJobAsync(JobStatus.Queued);
        var processing = await SeedJobAsync(JobStatus.Processing);

        await using var scope = _provider.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        (await unitOfWork.Jobs.FailIfQueuedAsync(queued.Id, "parked", Ct)).Should().BeTrue();
        (await unitOfWork.Jobs.FailIfQueuedAsync(processing.Id, "parked", Ct)).Should().BeFalse();

        await using var db = fixture.CreateDbContext();
        (await db.Jobs.SingleAsync(j => j.Id == queued.Id, Ct)).Status.Should().Be(JobStatus.Failed);
        (await db.Jobs.SingleAsync(j => j.Id == processing.Id, Ct)).Status.Should().Be(JobStatus.Processing);
    }
}
