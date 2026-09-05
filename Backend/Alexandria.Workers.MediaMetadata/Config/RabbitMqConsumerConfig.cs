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
    /// Queue exhausted trigger messages are parked on after
    /// <see cref="TriggerMaxStagingAttempts"/> staging failures.
    /// </summary>
    public string TriggerParkingQueueName { get; set; } = "media-metadata-trigger-parking-queue";

    /// <summary>
    /// Routing key (on the same exchange) exhausted triggers are published to.
    /// Must not collide with the <c>media-metadata.enrich.#</c> trigger binding.
    /// </summary>
    public string TriggerParkingRoutingKey { get; set; } = "media-metadata.trigger.parking";

    /// <summary>
    /// How many staging attempts a trigger gets before it is parked and
    /// surfaced as a worker-cycle failure instead of being requeued forever.
    /// </summary>
    public int TriggerMaxStagingAttempts { get; set; } = 5;

    /// <summary>
    /// Queue the completion messages arrive on.
    /// </summary>
    public string ResultsQueueName { get; set; } = "media-metadata-results-queue";

    public ushort PrefetchCount { get; set; } = 10;

    public bool AutoAck { get; set; }

    public int Concurrency { get; set; } = 1;
}