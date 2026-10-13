using Alexandria.Common.Config;
using Alexandria.Common.Services;
using Alexandria.Infrastructure;
using Alexandria.Infrastructure.DocumentWorker;
using Alexandria.Infrastructure.HealthChecks;
using Alexandria.Workers.Lyrics;
using Alexandria.Workers.Lyrics.Handlers;
using Alexandria.Workers.MediaMetadata.Workers;
using Alexandria.Workers.Playlist.Workers;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using LyricsServices = Alexandria.Workers.Lyrics.Extensions.WorkerExtensions;
using MetadataServices = Alexandria.Workers.MediaMetadata.Extensions.ServiceExtensions;
using PlaylistServices = Alexandria.Workers.Playlist.Extensions.ServiceExtensions;

namespace Alexandria.Tests.Unit.Infrastructure;

public class ProcessingCapabilityRegistrationTests
{
    private static IConfiguration Configuration(Dictionary<string, string?>? values = null)
    {
        values ??= [];
        values["ConnectionStrings:AlexandriaPostgres"] =
            "Host=localhost;Database=registration_only;Username=postgres;Password=postgres";
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddServices_binds_actual_features_and_preserves_missing_key_defaults(bool configured)
    {
        var values = new Dictionary<string, string?>();
        if (configured)
        {
            foreach (var name in new[] { "DocumentPreviews", "MediaPreviews", "AdaptiveStreaming", "AudioAnalysis", "Lyrics" })
                values[$"Features:{name}"] = "false";
        }

        var services = new ServiceCollection();
        services.AddSingleton(Configuration(values));
        services.AddServices();
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<ProcessingCapabilities>>().Value;

        options.DocumentPreviews.Should().Be(!configured);
        options.MediaPreviews.Should().Be(!configured);
        options.AdaptiveStreaming.Should().Be(!configured);
        options.AudioAnalysis.Should().Be(!configured);
        options.Lyrics.Should().Be(!configured);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddWorkerServices_audio_analysis_controls_all_coordinator_and_backfill_workers(bool enabled)
    {
        var configuration = Configuration(new()
        {
            ["Features:AudioAnalysis"] = enabled.ToString(),
            ["Features:Autotagging"] = "true"
        });
        var services = new ServiceCollection();

        MetadataServices.AddWorkerServices(services, configuration);

        var hostedTypes = services.Where(s => s.ServiceType == typeof(IHostedService))
            .Select(s => s.ImplementationType).ToArray();
        var pipelineTypes = new[]
        {
            typeof(TriggerConsumerWorker), typeof(AccumulatorWorker), typeof(ResultsConsumerWorker),
            typeof(TimeoutSweepWorker), typeof(AutoTagSweepWorker), typeof(AutoTagSyncWorker),
            typeof(EnrichmentBackfillWorker)
        };

        foreach (var type in pipelineTypes)
        {
            if (enabled)
                hostedTypes.Should().Contain(type);
            else
                hostedTypes.Should().NotContain(type);
        }
    }

    [Fact]
    public void AddWorkerServices_playlist_resolves_sync_and_sweep_without_storage_or_network_connection()
    {
        var configuration = Configuration();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWorkerDatabase(configuration);
        PlaylistServices.AddWorkerServices(services, configuration);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IAutoPlaylistSyncService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IAutoPlaylistSweepService>().Should().NotBeNull();
        var options = provider.GetRequiredService<IOptions<Alexandria.Workers.Playlist.Config.PlaylistConsumerConfig>>().Value;
        options.SyncQueueName.Should().Be("playlist-queue");
        options.SyncRoutingKey.Should().Be("playlist.sync");
        services.Should().Contain(s => s.ImplementationType == typeof(PlaylistSyncWorker));
        services.Should().Contain(s => s.ImplementationType == typeof(PlaylistSweepWorker));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public void AddWorkerServices_lyrics_requires_explicit_enable_and_resolves_provider_graph(string? flag, bool enabled)
    {
        var configuration = Configuration(new() { ["Features:Lyrics"] = flag });
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWorkerDatabase(configuration);
        LyricsServices.AddWorkerServices(services, configuration);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        if (enabled)
        {
            services.Should().Contain(s => s.ImplementationType == typeof(LyricsWorker));
            scope.ServiceProvider.GetRequiredService<LyricsHandler>().Should().NotBeNull();
            provider.GetRequiredService<ITrackLyricsProvider>().Should().NotBeNull();
        }
        else
        {
            services.Should().NotContain(s => s.ImplementationType == typeof(LyricsWorker));
            scope.ServiceProvider.GetService<LyricsHandler>().Should().BeNull();
        }
    }

    [Fact]
    public void AddHealthMonitoring_registers_exactly_the_installed_worker_set()
    {
        var values = new Dictionary<string, string?>();
        var names = new[] { "DocumentWorker", "MediaWorker", "TranspilationWorker", "LyricsWorker", "MediaMetadataWorker", "PlaylistWorker" };
        for (var i = 0; i < names.Length; i++)
        {
            values[$"HealthChecks:Workers:{i}:Name"] = names[i];
            values[$"HealthChecks:Workers:{i}:Url"] = $"http://{names[i]}/health";
            values[$"HealthChecks:Workers:{i}:Enabled"] = (i is 1 or 5).ToString();
        }

        var services = new ServiceCollection();
        services.AddHealthMonitoring(Configuration(values));
        using var provider = services.BuildServiceProvider();

        var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        registrations.Select(r => r.Name).Should().BeEquivalentTo("worker-mediaworker", "worker-playlistworker");
        registrations.Should().OnlyContain(r => r.Tags.Contains("worker"));
    }
}
