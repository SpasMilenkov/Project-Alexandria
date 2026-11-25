using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Amazon.S3;
using NSubstitute;
using Alexandria.Infrastructure.DocumentWorker;
using Alexandria.Infrastructure.Messaging;
using Alexandria.Infrastructure.Messaging.Outbox;
using Alexandria.Infrastructure.Workers;
using Alexandria.Repositories;
using Alexandria.Services.Storage.Content.Mime;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Alexandria.Tests.Unit.Infrastructure;

public class WorkerServiceRegistrationTests
{
    [Fact]
    public void AddCoreWorkerServices_resolves_unit_of_work_with_scoped_overview_summaries()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:AlexandriaPostgres"] =
                "Host=localhost;Database=registration_only;Username=postgres;Password=postgres"
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddWorkerDatabase(configuration);
        services.AddCoreWorkerServices();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var first = firstScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var second = secondScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        first.OverviewSummaries.Should().BeOfType<OverviewSummaryRepository>();
        first.OverviewSummaries.Should().BeSameAs(
            firstScope.ServiceProvider.GetRequiredService<IOverviewSummaryRepository>());
        second.OverviewSummaries.Should().NotBeSameAs(first.OverviewSummaries);
    }

    [Fact]
    public async Task AddRabbitMqAsync_registers_outbox_writer_relay_and_delivery_retry_without_scope_leaks()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:AlexandriaPostgres"] =
                "Host=localhost;Database=registration_only;Username=postgres;Password=postgres"
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(Substitute.For<IAmazonS3>());
        services.AddKeyedSingleton("public", Substitute.For<IAmazonS3>());
        services.AddWorkerDatabase(configuration);
        services.AddCoreWorkerServices();
        services.AddRabbitMqAsync(configuration, ServiceType.Lyrics);

        // ValidateOnBuild is what a Development host does, so a singleton holding a scoped
        // service fails here rather than at worker startup.
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        await using var scope = provider.CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<IOutbox>().Should().BeOfType<OutboxWriter>();
        scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().Should().NotBeNull();
        provider.GetRequiredService<DeliveryRetry>().Should().NotBeNull();
        provider.GetServices<IHostedService>().OfType<OutboxRelay>().Should().ContainSingle();
    }

    [Fact]
    public void AddCoreWorkerServices_resolves_mime_detection()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:AlexandriaPostgres"] =
                "Host=localhost;Database=registration_only;Username=postgres;Password=postgres"
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddWorkerDatabase(configuration);
        services.AddCoreWorkerServices();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        var resolver = scope.ServiceProvider.GetRequiredService<IMimeTypeResolver>();
        var resolution = resolver.Resolve(
            "photo.png",
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52],
            "application/octet-stream");

        resolver.Should().BeOfType<MimeTypeResolver>();
        resolution.MimeType.Should().Be("image/png");
        resolution.Source.Should().Be(MimeSource.SniffedSpecific);
    }
}
