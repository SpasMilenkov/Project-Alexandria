using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Alexandria.Infrastructure.Messaging;

/// <summary>
/// Acks and nacks that survive a closed channel. When the connection drops, the broker requeues
/// every unacknowledged delivery by itself, so a settle that cannot be sent is not a failure of
/// the work. Treating it as one sends finished work into retry, or stops the host when the settle
/// runs outside a consumer callback.
/// </summary>
public static partial class DeliverySettlement
{
    /// <summary>Acks one delivery. False when the channel was closed and the broker keeps it.</summary>
    public static async Task<bool> AckAsync(
        IChannel channel,
        ulong deliveryTag,
        ILogger logger,
        CancellationToken ct)
    {
        try
        {
            await channel.BasicAckAsync(deliveryTag, multiple: false, ct);
            return true;
        }
        catch (AlreadyClosedException ex)
        {
            LogAckLost(logger, deliveryTag, ReasonOf(ex));
            return false;
        }
    }

    /// <summary>Nacks one delivery. False when the channel was closed and the broker keeps it.</summary>
    public static async Task<bool> NackAsync(
        IChannel channel,
        ulong deliveryTag,
        bool requeue,
        ILogger logger,
        CancellationToken ct)
    {
        try
        {
            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue, ct);
            return true;
        }
        catch (AlreadyClosedException ex)
        {
            LogNackLost(logger, deliveryTag, ReasonOf(ex));
            return false;
        }
    }

    private static string ReasonOf(AlreadyClosedException ex)
        => ex.ShutdownReason?.ReplyText ?? ex.Message;

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Delivery {DeliveryTag} was not acknowledged because the channel is closed ({Reason}); the broker requeues it.")]
    private static partial void LogAckLost(ILogger logger, ulong deliveryTag, string reason);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Delivery {DeliveryTag} was not rejected because the channel is closed ({Reason}); the broker requeues it.")]
    private static partial void LogNackLost(ILogger logger, ulong deliveryTag, string reason);
}
