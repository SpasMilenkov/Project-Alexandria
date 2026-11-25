using System.Text;
using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Infrastructure.Messaging;
using Alexandria.Workers.MediaTranspilation;
using Alexandria.Workers.MediaTranspilation.Handlers;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RabbitMQ.Client;

namespace Alexandria.Tests.Unit.Infrastructure;

public class TranspilationWorkerRetryTests
{
    private const ulong Tag = 9;

    private static readonly TimeSpan HangLimit = TimeSpan.FromSeconds(10);

    private readonly IChannel _channel = Substitute.For<IChannel>();
    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();
    private readonly IPublisherService _publisher = Substitute.For<IPublisherService>();

    private readonly TaskCompletionSource<IAsyncBasicConsumer> _consumer =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private TranspilationWorker CreateSut()
    {
        _channel.QueueDeclareAsync(default!, default, default, default, default, default, default,
                TestContext.Current.CancellationToken)
            .ReturnsForAnyArgs(new QueueDeclareOk("transpilation-queue", 0, 0));
        _channel.BasicConsumeAsync(default!, default, default!, default, default, default, default!,
                TestContext.Current.CancellationToken)
            .ReturnsForAnyArgs(call =>
            {
                _consumer.TrySetResult(call.ArgAt<IAsyncBasicConsumer>(6));
                return Task.FromResult("tag");
            });

        var connection = Substitute.For<IConnection>();
        connection.CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>()).Returns(_channel);

        var broker = new MessageBrokerConnection(isEnabled: true);
        broker.SetConnection(connection);

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Jobs.Returns(_jobs);

        // Stands in for anything that fails before the handler claims the job, such as the
        // database being unreachable.
        var scoped = Substitute.For<IServiceProvider>();
        scoped.GetService(typeof(TranspilationJobHandler))
            .Returns(_ => throw new InvalidOperationException("database unreachable"));
        scoped.GetService(typeof(IUnitOfWork)).Returns(unitOfWork);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(scoped);

        var factory = Substitute.For<IServiceScopeFactory>();
        factory.CreateScope().Returns(scope);

        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(IServiceScopeFactory)).Returns(factory);

        var retry = new DeliveryRetry(_publisher, NullLogger<DeliveryRetry>.Instance);

        return new TranspilationWorker(
            NullLogger<TranspilationWorker>.Instance,
            broker,
            new ConfigurationBuilder().Build(),
            provider,
            retry);
    }

    private static IReadOnlyBasicProperties PropsWithAttempts(int attempts)
    {
        var props = Substitute.For<IReadOnlyBasicProperties>();
        props.Headers.Returns(new Dictionary<string, object?> { [DeliveryRetry.AttemptsHeader] = attempts });
        return props;
    }

    [Fact]
    public async Task failure_before_the_claim_on_the_last_attempt_parks_and_fails_the_queued_job()
    {
        var ct = TestContext.Current.CancellationToken;
        var jobId = Guid.NewGuid();

        var worker = CreateSut();
        await worker.StartAsync(ct);
        var consumer = await _consumer.Task.WaitAsync(HangLimit, ct);

        // Four stored attempts make this the fifth, which reaches the default limit and parks.
        await consumer.HandleBasicDeliverAsync(
            "tag", Tag, true, "content-exchange", "transpilation.job",
            PropsWithAttempts(4), Encoding.UTF8.GetBytes(jobId.ToString()), CancellationToken.None);

        await _jobs.Received(1).FailIfQueuedAsync(jobId, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _channel.Received(1).BasicAckAsync(Tag, false, Arg.Any<CancellationToken>());
        await _channel.DidNotReceiveWithAnyArgs().BasicNackAsync(default, default, default, ct);
        await _publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, ct);

        await worker.StopAsync(ct);
    }
}
