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
    /// Queue single-playlist rebuild requests arrive on.
    /// </summary>
    public string SyncQueueName { get; set; } = "playlist-queue";

    /// <summary>
    /// Routing key for rebuild requests (one playlist id per message).
    /// </summary>
    public string SyncRoutingKey { get; set; } = "playlist.sync";

    public ushort PrefetchCount { get; set; } = 10;

    /// <summary>
    /// How often the backstop sweep looks for stale auto-playlists.
    /// </summary>
    public int SweepIntervalSeconds { get; set; } = 900;
}