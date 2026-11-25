using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Common.Settings;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Infrastructure.Messaging.Outbox;
using Alexandria.Tests.Common.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.Infrastructure;

public class OutboxRelayTests
{
    [Fact]
    public async Task failing_prune_waits_for_its_next_slot_instead_of_blocking_the_send()
    {
        var ct = TestContext.Current.CancellationToken;
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var messages = Substitute.For<IOutboxMessageRepository>();
        var claimed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        unitOfWork.OutboxMessages.Returns(messages);
        messages.DeleteSentBeforeAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(new TimeoutException("statement timeout")));
        messages.ClaimDueAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                claimed.TrySetResult();
                return Task.FromResult<IReadOnlyList<OutboxMessage>>([]);
            });

        var broker = new FakeMessageBrokerStatus();
        var options = Options.Create(new OutboxOptions { PollIntervalSeconds = 60 });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(unitOfWork);
        services.AddSingleton(Substitute.For<IPublisherService>());
        services.AddSingleton<IMessageBrokerStatus>(broker);
        services.AddSingleton(options);
        services.AddSingleton(new OutboxHost { Service = ServiceType.Api });
        services.AddScoped<OutboxDispatcher>();
        await using var provider = services.BuildServiceProvider();

        // A pending wake-up starts the second cycle at once instead of after the poll interval.
        var signal = new OutboxSignal();
        signal.Notify();

        var relay = new OutboxRelay(provider.GetRequiredService<IServiceScopeFactory>(), signal, broker, options,
            NullLogger<OutboxRelay>.Instance);

        await relay.StartAsync(ct);
        await claimed.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        await relay.StopAsync(ct);

        await messages.Received(1).DeleteSentBeforeAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }
}
