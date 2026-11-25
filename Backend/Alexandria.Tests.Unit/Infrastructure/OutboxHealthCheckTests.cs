using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Common.Settings;
using Alexandria.Infrastructure.HealthChecks;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.Infrastructure;

public class OutboxHealthCheckTests
{
    private readonly IOutboxMessageRepository _messages = Substitute.For<IOutboxMessageRepository>();
    private readonly IMessageBrokerStatus _broker = Substitute.For<IMessageBrokerStatus>();

    public OutboxHealthCheckTests()
    {
        _broker.IsAvailable.Returns(true);
    }

    private OutboxHealthCheck CreateSut()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.OutboxMessages.Returns(_messages);

        var services = new ServiceCollection();
        services.AddScoped(_ => unitOfWork);

        return new OutboxHealthCheck(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            _broker,
            Options.Create(new OutboxOptions { BacklogAlertMinutes = 5 }));
    }

    private Task<HealthCheckResult> CheckAsync() =>
        CreateSut().CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task empty_outbox_is_healthy()
    {
        _messages.GetOldestPendingCreatedAtAsync(Arg.Any<CancellationToken>()).Returns((DateTime?)null);

        (await CheckAsync()).Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task recent_backlog_is_healthy()
    {
        _messages.GetOldestPendingCreatedAtAsync(Arg.Any<CancellationToken>())
            .Returns(DateTime.UtcNow.AddMinutes(-1));

        (await CheckAsync()).Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task backlog_older_than_the_alert_is_unhealthy()
    {
        _messages.GetOldestPendingCreatedAtAsync(Arg.Any<CancellationToken>())
            .Returns(DateTime.UtcNow.AddMinutes(-20));

        var result = await CheckAsync();

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("waiting");
    }

    [Fact]
    public async Task backlog_during_a_broker_outage_is_left_to_the_rabbitmq_check()
    {
        _broker.IsAvailable.Returns(false);
        _messages.GetOldestPendingCreatedAtAsync(Arg.Any<CancellationToken>())
            .Returns(DateTime.UtcNow.AddHours(-1));

        (await CheckAsync()).Status.Should().Be(HealthStatus.Healthy);
        await _messages.DidNotReceiveWithAnyArgs().GetOldestPendingCreatedAtAsync(TestContext.Current.CancellationToken);
    }
}
