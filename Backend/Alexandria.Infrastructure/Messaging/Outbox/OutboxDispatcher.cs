using Alexandria.Common;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Services;
using Alexandria.Common.Settings;
using Alexandria.Data.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client.Exceptions;

namespace Alexandria.Infrastructure.Messaging.Outbox;

/// <summary>
/// One relay cycle: claims due messages, sends each with broker confirms and records the outcome,
/// all in one transaction so the claim locks hold until the outcomes are saved.
/// </summary>
public sealed partial class OutboxDispatcher(
    IUnitOfWork unitOfWork,
    IPublisherService publisher,
    IMessageBrokerStatus broker,
    OutboxHost host,
    IOptions<OutboxOptions> options,
    ILogger<OutboxDispatcher> logger)
{
    internal const string ServiceInstance = "outbox";
    internal const string DeadMessageError = "its message could not be delivered to a worker queue";

    /// <returns>True when a full batch went out and more messages may be waiting.</returns>
    public async Task<bool> DispatchDueAsync(DateTime now, CancellationToken ct)
    {
        var config = options.Value;
        var batchSize = Math.Max(config.BatchSize, 1);

        Exception? lastDeath = null;
        var interrupted = false;
        IReadOnlyList<OutboxMessage> due;

        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            due = await unitOfWork.OutboxMessages.ClaimDueAsync(now, batchSize, ct);

            foreach (var message in due)
            {
                try
                {
                    await publisher.PublishAsync(new OutgoingMessage
                    {
                        RoutingKey = message.RoutingKey,
                        Body = message.Body,
                        MessageId = message.Id.ToString()
                    }, ct);

                    message.SentAt = now;
                    message.UpdatedAt = now;
                }
                catch (Exception ex) when (!IsStopping(ex, ct))
                {
                    // A lost connection says nothing about the message itself, so it costs no
                    // attempt; the rest of the batch waits for the next cycle.
                    if (IsConnectionLost(ex))
                    {
                        LogConnectionLost(logger, ex, message.Id);
                        interrupted = true;
                        break;
                    }

                    // With the connection still up the failure belongs to this message. Counting
                    // it lets a message that always fails die instead of blocking the rows behind it.
                    if (!RecordFailure(message, ex, now, config))
                        continue;

                    lastDeath = ex;

                    if (message.JobId is { } jobId)
                        await unitOfWork.Jobs.FailIfQueuedAsync(jobId, DeadMessageError, ct);
                }
            }

            await unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }

        if (lastDeath is not null)
            await RecordDeathAsync(lastDeath, ct);

        return !interrupted && due.Count == batchSize;
    }

    public async Task<int> PruneSentAsync(DateTime now, CancellationToken ct)
    {
        var retention = TimeSpan.FromDays(Math.Max(options.Value.RetentionDays, 1));

        return await unitOfWork.OutboxMessages.DeleteSentBeforeAsync(now - retention, ct);
    }

    internal static TimeSpan RetryDelay(OutboxOptions config, int attempts)
    {
        var baseDelay = TimeSpan.FromSeconds(Math.Max(config.RetryBaseDelaySeconds, 1));
        var maxDelay = TimeSpan.FromSeconds(Math.Max(config.RetryMaxDelaySeconds, config.RetryBaseDelaySeconds));
        var delay = baseDelay * Math.Pow(2, Math.Clamp(attempts - 1, 0, 20));

        return delay < maxDelay ? delay : maxDelay;
    }

    // The RabbitMQ client reports its own RPC and confirm timeouts as cancellations. Only ours
    // should abort the cycle; a timeout is a failed send, or the earlier rows would lose SentAt
    // in the rollback and go out again.
    private static bool IsStopping(Exception ex, CancellationToken ct)
        => ex is OperationCanceledException && ct.IsCancellationRequested;

    private bool IsConnectionLost(Exception ex)
    {
        if (ex is PublishException)
            return false;

        return ex is MessageBrokerUnavailableException || !broker.IsAvailable;
    }

    /// <returns>True when the failure used up the last attempt and the message is now dead.</returns>
    private bool RecordFailure(OutboxMessage message, Exception ex, DateTime now, OutboxOptions config)
    {
        message.Attempts++;
        message.LastError = Truncate(Describe(message, ex), ValidationConstants.StringLengths.ExtraLongString);
        message.UpdatedAt = now;

        if (message.Attempts >= Math.Max(config.MaxAttempts, 1))
        {
            message.DeadAt = now;
            LogDead(logger, ex, message.Id, message.RoutingKey, message.Attempts);
            return true;
        }

        message.AvailableAt = now + RetryDelay(config, message.Attempts);
        LogSendFailed(logger, ex, message.Id, message.RoutingKey, message.Attempts);
        return false;
    }

    private async Task RecordDeathAsync(Exception ex, CancellationToken ct)
    {
        try
        {
            await WorkerCycleFailureRecorder.RecordAsync(unitOfWork, host.Service, ServiceInstance, ex, ct);
        }
        catch (Exception recordEx) when (recordEx is not OperationCanceledException)
        {
            // The message is already marked dead and shows up in the admin panel either way.
            LogIncidentFailed(logger, recordEx);
        }
    }

    private static string Describe(OutboxMessage message, Exception ex)
    {
        if (ex is OperationCanceledException)
            return $"The broker did not answer in time: {ex.Message}";

        if (ex is not PublishException publishEx)
            return $"Sending failed while the broker connection stayed up: {ex.Message}";

        if (publishEx.IsReturn)
            return $"No queue is bound for routing key '{message.RoutingKey}': {ex.Message}";

        return $"The broker refused the message: {ex.Message}";
    }

    private static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..maxLength];
}
