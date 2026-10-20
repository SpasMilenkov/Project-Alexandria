namespace Alexandria.Workers.Playlist.Config;

/// <summary>
/// RabbitMQ consumer settings for the playlist worker.
/// Bind from <c>appsettings.json</c> under the <c>"RabbitMQ:Consumer"</c> section.
/// </summary>
public class PlaylistConsumerConfig
{
    /// <summary>
    /// Content exchange all worker queues bind to.
    /// </summary>
    public string ExchangeName { get; set; } = "content-exchange";

    /// <summary>
    /// Queue file-scoped playlist sync requests arrive on.
    /// </summary>
    public string SyncQueueName { get; set; } = "playlist-queue";

    /// <summary>
    /// Routing key for file sync requests (one file and owner per message).
    /// </summary>
    public string SyncRoutingKey { get; set; } = "playlist.sync";

    /// <summary>
    /// Maximum unacknowledged deliveries. Must cover the largest batch because
    /// the worker holds messages until the owner batch has been applied.
    /// </summary>
    public ushort PrefetchCount { get; set; } = 100;

    /// <summary>
    /// Maximum number of distinct files in one coalesced owner batch.
    /// </summary>
    public int MaxBatchSize { get; set; } = 100;

    /// <summary>
    /// How long the drainer waits for more file events before flushing a partial batch.
    /// </summary>
    public int CoalesceWindowMilliseconds { get; set; } = 10_000;

    /// <summary>
    /// How often the backstop sweep looks for stale auto-playlists.
    /// </summary>
    public int SweepIntervalSeconds { get; set; } = 900;
}