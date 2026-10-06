using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Infrastructure.DocumentWorker;
using Alexandria.Infrastructure.Workers;
using Alexandria.Repositories;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
}
