using Alexandria.Common;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Common.Settings;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Infrastructure.Messaging.Outbox;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using RabbitMQ.Client.Exceptions;

namespace Alexandria.Tests.Unit.Infrastructure;

public class OutboxDispatcherTests
{
    private static readonly DateTime Now = new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IOutboxMessageRepository _messages = Substitute.For<IOutboxMessageRepository>();
    private readonly IOperationalEventRepository _events = Substitute.For<IOperationalEventRepository>();
    private readonly IPublisherService _publisher = Substitute.For<IPublisherService>();
    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();
    private readonly IMessageBrokerStatus _broker = Substitute.For<IMessageBrokerStatus>();

    private readonly OutboxOptions _options = new()
    {
        BatchSize = 2,
        MaxAttempts = 3,
        RetryBaseDelaySeconds = 5,
        RetryMaxDelaySeconds = 60,
        RetentionDays = 7
    };

    public OutboxDispatcherTests()
    {
        _unitOfWork.OutboxMessages.Returns(_messages);
        _unitOfWork.OperationalEvents.Returns(_events);
        _unitOfWork.Jobs.Returns(_jobs);
        _broker.IsAvailable.Returns(true);
    }

    private OutboxDispatcher CreateSut() =>
        new(_unitOfWork, _publisher, _broker, new OutboxHost { Service = ServiceType.Lyrics },
            Options.Create(_options), NullLogger<OutboxDispatcher>.Instance);

    private static OutboxMessage Message(int attempts = 0, Guid? jobId = null) => new()
    {
        Id = Guid.NewGuid(),
        RoutingKey = "lyrics.job",
        Body = [1, 2, 3],
        JobId = jobId,
        Attempts = attempts,
        AvailableAt = Now,
        CreatedAt = Now
    };

    private void GivenDue(params OutboxMessage[] messages) =>
        _messages.ClaimDueAsync(Now, _options.BatchSize, Arg.Any<CancellationToken>()).Returns(messages);

    private void PublishFails(Exception ex) =>
        _publisher.PublishAsync(Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(ex));

    [Fact]
    public async Task dispatch_sends_each_message_with_its_id_and_marks_it_sent()
    {
        var message = Message();
        GivenDue(message);

        var more = await CreateSut().DispatchDueAsync(Now, TestContext.Current.CancellationToken);

        more.Should().BeFalse();
        message.SentAt.Should().Be(Now);
        message.DeadAt.Should().BeNull();
        await _publisher.Received(1).PublishAsync(
            Arg.Is<OutgoingMessage>(m => m.RoutingKey == "lyrics.job"
                                         && m.MessageId == message.Id.ToString()
                                         && m.Body.SequenceEqual(message.Body)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task dispatch_claims_and_saves_outcomes_inside_one_transaction()
    {
        GivenDue(Message());

        await CreateSut().DispatchDueAsync(Now, TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>());
            _messages.ClaimDueAsync(Now, _options.BatchSize, Arg.Any<CancellationToken>());
            _publisher.PublishAsync(Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>());
            _unitOfWork.CommitAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task refused_message_counts_an_attempt_and_waits_before_the_next_try()
    {
        var message = Message();
        GivenDue(message);
        PublishFails(new PublishException(1, isReturn: true));

        await CreateSut().DispatchDueAsync(Now, TestContext.Current.CancellationToken);

        message.Attempts.Should().Be(1);
        message.SentAt.Should().BeNull();
        message.DeadAt.Should().BeNull();
        message.AvailableAt.Should().Be(Now.AddSeconds(5));
        message.LastError.Should().Contain("No queue is bound for routing key 'lyrics.job'");
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task message_refused_on_its_last_attempt_is_dead_and_raises_an_incident_for_this_host()
    {
        var message = Message(attempts: 2);
        GivenDue(message);
        PublishFails(new PublishException(1, isReturn: false));

        await CreateSut().DispatchDueAsync(Now, TestContext.Current.CancellationToken);

        message.Attempts.Should().Be(3);
        message.DeadAt.Should().Be(Now);
        message.LastError.Should().StartWith("The broker refused the message");
        await _events.Received(1).AddActiveIfAbsentAsync(
            Arg.Is<OperationalEvent>(e => e.Code == OperationalEventCode.WorkerCycleFailure
                                          && e.ServiceType == ServiceType.Lyrics),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task lost_connection_costs_no_attempt_and_leaves_the_rest_for_the_next_cycle()
    {
        var first = Message();
        var second = Message();
        GivenDue(first, second);
        PublishFails(new MessageBrokerUnavailableException(disabled: false));

        var more = await CreateSut().DispatchDueAsync(Now, TestContext.Current.CancellationToken);

        more.Should().BeFalse();
        first.Attempts.Should().Be(0);
        first.AvailableAt.Should().Be(Now);
        first.SentAt.Should().BeNull();
        await _publisher.Received(1).PublishAsync(Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>());
        await _events.DidNotReceiveWithAnyArgs().AddActiveIfAbsentAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task failure_after_the_connection_dropped_costs_no_attempt()
    {
        var message = Message();
        GivenDue(message, Message());
        _broker.IsAvailable.Returns(false);
        PublishFails(new InvalidOperationException("channel closed"));

        var more = await CreateSut().DispatchDueAsync(Now, TestContext.Current.CancellationToken);

        more.Should().BeFalse();
        message.Attempts.Should().Be(0);
        message.AvailableAt.Should().Be(Now);
        await _publisher.Received(1).PublishAsync(Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task failure_with_the_connection_still_up_counts_an_attempt_and_moves_on()
    {
        var failing = Message();
        var next = Message();
        GivenDue(failing, next);
        _publisher.PublishAsync(Arg.Is<OutgoingMessage>(m => m.MessageId == failing.Id.ToString()),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("channel closed by the broker")));

        await CreateSut().DispatchDueAsync(Now, TestContext.Current.CancellationToken);

        failing.Attempts.Should().Be(1);
        failing.SentAt.Should().BeNull();
        failing.AvailableAt.Should().Be(Now.AddSeconds(5));
        failing.LastError.Should().Contain("channel closed by the broker");
        next.SentAt.Should().Be(Now);
    }

    [Fact]
    public async Task message_that_keeps_failing_with_the_connection_up_eventually_dies()
    {
        var message = Message(attempts: 2);
        GivenDue(message);
        PublishFails(new InvalidOperationException("channel closed by the broker"));

        await CreateSut().DispatchDueAsync(Now, TestContext.Current.CancellationToken);

        message.Attempts.Should().Be(3);
        message.DeadAt.Should().Be(Now);
    }

    [Fact]
    public async Task dead_message_fails_the_queued_job_it_starts_inside_the_transaction()
    {
        var jobId = Guid.NewGuid();
        GivenDue(Message(attempts: 2, jobId: jobId));
        PublishFails(new PublishException(1, isReturn: true));

        await CreateSut().DispatchDueAsync(Now, TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            _jobs.FailIfQueuedAsync(jobId, OutboxDispatcher.DeadMessageError, Arg.Any<CancellationToken>());
            _unitOfWork.CommitAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task refusal_with_attempts_left_leaves_the_job_alone()
    {
        GivenDue(Message(jobId: Guid.NewGuid()));
        PublishFails(new PublishException(1, isReturn: true));

        await CreateSut().DispatchDueAsync(Now, TestContext.Current.CancellationToken);

        await _jobs.DidNotReceiveWithAnyArgs().FailIfQueuedAsync(default, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task dead_message_without_a_job_touches_no_job()
    {
        GivenDue(Message(attempts: 2));
        PublishFails(new PublishException(1, isReturn: true));

        await CreateSut().DispatchDueAsync(Now, TestContext.Current.CancellationToken);

        await _jobs.DidNotReceiveWithAnyArgs().FailIfQueuedAsync(default, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task full_batch_reports_that_more_may_be_waiting()
    {
        GivenDue(Message(), Message());

        var more = await CreateSut().DispatchDueAsync(Now, TestContext.Current.CancellationToken);

        more.Should().BeTrue();
    }

    [Fact]
    public async Task stopping_rolls_back_so_claimed_rows_are_released()
    {
        using var stopping = new CancellationTokenSource();
        GivenDue(Message());
        _publisher.PublishAsync(Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                stopping.Cancel();
                return Task.FromException(new OperationCanceledException(stopping.Token));
            });

        var act = () => CreateSut().DispatchDueAsync(Now, stopping.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        await _unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task broker_timeout_counts_an_attempt_and_keeps_the_rows_already_sent()
    {
        var sent = Message();
        var timedOut = Message();
        GivenDue(sent, timedOut);
        _publisher.PublishAsync(Arg.Is<OutgoingMessage>(m => m.MessageId == timedOut.Id.ToString()),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new OperationCanceledException("confirm timed out")));

        await CreateSut().DispatchDueAsync(Now, TestContext.Current.CancellationToken);

        sent.SentAt.Should().Be(Now);
        timedOut.Attempts.Should().Be(1);
        timedOut.SentAt.Should().BeNull();
        timedOut.LastError.Should().StartWith("The broker did not answer in time");
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task prune_deletes_sent_rows_older_than_the_retention()
    {
        await CreateSut().PruneSentAsync(Now, TestContext.Current.CancellationToken);

        await _messages.Received(1).DeleteSentBeforeAsync(Now.AddDays(-7), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(4, 40)]
    [InlineData(5, 60)]
    [InlineData(9, 60)]
    public void retry_delay_doubles_per_refusal_up_to_the_cap(int attempts, int expectedSeconds)
    {
        OutboxDispatcher.RetryDelay(_options, attempts).Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }
}
