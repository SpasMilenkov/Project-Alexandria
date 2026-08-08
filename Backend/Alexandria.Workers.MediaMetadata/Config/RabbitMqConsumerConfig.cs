namespace Alexandria.Workers.MediaMetadata.Config;

/// <summary>
/// RabbitMQ consumer settings for the enrichment worker.
/// Bind from <c>appsettings.json</c> under the <c>"RabbitMQ:Consumer"</c> section.
/// </summary>
public class RabbitMqConsumerConfig
{
    /// <summary>
    /// Content exchange all worker queues bind to.
    /// </summary>
    public string ExchangeName { get; set; } = "content-exchange";

    /// <summary>
    /// Queue the trigger messages arrive on.
    /// </summary>
    public string TriggerQueueName { get; set; } = "media-metadata-trigger-queue";

    /// <summary>
    /// Queue the completion messages arrive on.
    /// </summary>
    public string ResultsQueueName { get; set; } = "media-metadata-results-queue";

    public ushort PrefetchCount { get; set; } = 10;

    public bool AutoAck { get; set; }

    public int Concurrency { get; set; } = 1;
}