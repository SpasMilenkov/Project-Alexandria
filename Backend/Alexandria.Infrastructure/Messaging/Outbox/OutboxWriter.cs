using System.Text;
using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Microsoft.Extensions.Logging;

namespace Alexandria.Infrastructure.Messaging.Outbox;

public sealed partial class OutboxWriter(
    IUnitOfWork unitOfWork,
    IMessageBrokerStatus broker,
    IOutboxSignal signal,
    ILogger<OutboxWriter> logger) : IOutbox
{
    public Task EnqueueAsync(string routingKey, byte[] body, CancellationToken ct)
        => AddAsync(routingKey, body, null, ct);

    public Task EnqueueJobAsync(string routingKey, Guid jobId, CancellationToken ct)
        => AddAsync(routingKey, Encoding.UTF8.GetBytes(jobId.ToString()), jobId, ct);

    private async Task AddAsync(string routingKey, byte[] body, Guid? jobId, CancellationToken ct)
    {
        // Without a broker nothing would ever send the row; keeping it would only grow the table.
        if (!broker.IsEnabled)
        {
            LogDroppedBrokerDisabled(logger, routingKey);
            return;
        }

        // Outside a transaction a later save could write the row without the change it announces,
        // or the row could be lost because nothing saves it.
        if (!unitOfWork.HasActiveTransaction)
            throw new InvalidOperationException(
                "Outbox messages must be enqueued inside a unit-of-work transaction so they commit with the change they announce.");

        var now = DateTime.UtcNow;

        await unitOfWork.OutboxMessages.AddAsync(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            RoutingKey = routingKey,
            Body = body,
            JobId = jobId,
            AvailableAt = now,
            CreatedAt = now
        }, ct);

        unitOfWork.AfterCommit(signal.Notify);
    }
}
