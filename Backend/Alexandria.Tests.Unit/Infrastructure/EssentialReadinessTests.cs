using Alexandria.Infrastructure.HealthChecks;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.Infrastructure;

public class EssentialReadinessTests
{
    [Fact]
    public async Task CheckHealthAsync_optionalWorkersUnavailable_coreReadinessHealthyAndMonitoringUnhealthy()
    {
        using var provider = CreateSut("worker-document", "worker-media");
        var service = provider.GetRequiredService<HealthCheckService>();

        var readiness = await service.CheckHealthAsync(
            HealthMonitoringExtension.IsEssentialReadinessCheck, TestContext.Current.CancellationToken);
        var monitoring = await service.CheckHealthAsync(TestContext.Current.CancellationToken);

        readiness.Status.Should().Be(HealthStatus.Healthy);
        readiness.Entries.Keys.Should().BeEquivalentTo("postgres", "storage", "rabbitmq");
        monitoring.Status.Should().Be(HealthStatus.Unhealthy);
        monitoring.Entries["worker-document"].Status.Should().Be(HealthStatus.Unhealthy);
        monitoring.Entries["worker-media"].Status.Should().Be(HealthStatus.Unhealthy);
        monitoring.Entries.Should().NotContainKey("worker-playlist");
    }

    [Theory]
    [InlineData("postgres")]
    [InlineData("storage")]
    [InlineData("rabbitmq")]
    public async Task CheckHealthAsync_essentialDependencyUnavailable_readinessRemainsUnhealthy(string unavailable)
    {
        using var provider = CreateSut(unavailable);
        var service = provider.GetRequiredService<HealthCheckService>();

        var readiness = await service.CheckHealthAsync(
            HealthMonitoringExtension.IsEssentialReadinessCheck, TestContext.Current.CancellationToken);

        readiness.Status.Should().Be(HealthStatus.Unhealthy);
        readiness.Entries[unavailable].Status.Should().Be(HealthStatus.Unhealthy);
        readiness.Entries.Keys.Should().BeEquivalentTo("postgres", "storage", "rabbitmq");
    }

    private static ServiceProvider CreateSut(params string[] unavailable)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HealthChecks:Database:Enabled"] = "true",
            ["HealthChecks:Storage:Enabled"] = "true",
            ["HealthChecks:MessageBroker:Enabled"] = "true",
            ["HealthChecks:Workers:0:Name"] = "Document",
            ["HealthChecks:Workers:0:Url"] = "http://unavailable-document/health",
            ["HealthChecks:Workers:0:Enabled"] = "true",
            ["HealthChecks:Workers:1:Name"] = "Media",
            ["HealthChecks:Workers:1:Url"] = "http://unavailable-media/health",
            ["HealthChecks:Workers:1:Enabled"] = "true",
            ["HealthChecks:Workers:2:Name"] = "Playlist",
            ["HealthChecks:Workers:2:Url"] = "http://unavailable-playlist/health",
            ["HealthChecks:Workers:2:Enabled"] = "false",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHealthMonitoring(configuration);
        services.PostConfigure<HealthCheckServiceOptions>(options =>
        {
            var registrations = options.Registrations.ToArray();
            options.Registrations.Clear();

            foreach (var registration in registrations)
            {
                var check = Substitute.For<IHealthCheck>();
                var result = HealthCheckResult.Healthy();
                if (unavailable.Contains(registration.Name))
                    result = HealthCheckResult.Unhealthy();

                check.CheckHealthAsync(Arg.Any<HealthCheckContext>(), Arg.Any<CancellationToken>()).Returns(result);
                options.Registrations.Add(new HealthCheckRegistration(
                    registration.Name, _ => check, registration.FailureStatus, registration.Tags, registration.Timeout));
            }
        });

        return services.BuildServiceProvider();
    }
}
