using System.Text;
using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Infrastructure.Messaging.Outbox;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Alexandria.Tests.Unit.Infrastructure;

public class OutboxWriterTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IOutboxMessageRepository _messages = Substitute.For<IOutboxMessageRepository>();
    private readonly IMessageBrokerStatus _broker = Substitute.For<IMessageBrokerStatus>();
    private readonly IOutboxSignal _signal = Substitute.For<IOutboxSignal>();

    public OutboxWriterTests()
    {
        _unitOfWork.OutboxMessages.Returns(_messages);
        _unitOfWork.HasActiveTransaction.Returns(true);
        _broker.IsEnabled.Returns(true);
    }

    private OutboxWriter CreateSut() => new(_unitOfWork, _broker, _signal, NullLogger<OutboxWriter>.Instance);

    [Fact]
    public async Task enqueue_adds_a_due_row_without_saving_it()
    {
        await CreateSut().EnqueueAsync("lyrics.job", [7, 8], TestContext.Current.CancellationToken);

        await _messages.Received(1).AddAsync(
            Arg.Is<OutboxMessage>(m => m.RoutingKey == "lyrics.job"
                                       && m.Body.SequenceEqual(new byte[] { 7, 8 })
                                       && m.Id != Guid.Empty
                                       && m.AvailableAt == m.CreatedAt
                                       && m.SentAt == null
                                       && m.JobId == null
                                       && m.Attempts == 0),
            Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task enqueue_job_sends_the_job_id_as_the_body_and_links_the_row_to_the_job()
    {
        var jobId = Guid.NewGuid();

        await CreateSut().EnqueueJobAsync("lyrics.job", jobId, TestContext.Current.CancellationToken);

        await _messages.Received(1).AddAsync(
            Arg.Is<OutboxMessage>(m => m.RoutingKey == "lyrics.job"
                                       && Encoding.UTF8.GetString(m.Body) == jobId.ToString()
                                       && m.JobId == jobId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task enqueue_wakes_the_relay_only_after_the_commit()
    {
        Action? afterCommit = null;
        _unitOfWork.When(u => u.AfterCommit(Arg.Any<Action>())).Do(call => afterCommit = call.Arg<Action>());

        await CreateSut().EnqueueAsync("lyrics.job", [1], TestContext.Current.CancellationToken);

        _signal.DidNotReceive().Notify();
        afterCommit.Should().NotBeNull();

        afterCommit!();
        _signal.Received(1).Notify();
    }

    [Fact]
    public async Task enqueue_outside_a_transaction_is_refused()
    {
        _unitOfWork.HasActiveTransaction.Returns(false);

        var act = () => CreateSut().EnqueueAsync("lyrics.job", [1], TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _messages.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task enqueue_with_the_broker_turned_off_drops_the_message()
    {
        _broker.IsEnabled.Returns(false);
        _unitOfWork.HasActiveTransaction.Returns(false);

        await CreateSut().EnqueueAsync("lyrics.job", [1], TestContext.Current.CancellationToken);

        await _messages.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
        _unitOfWork.DidNotReceiveWithAnyArgs().AfterCommit(default!);
    }
}
