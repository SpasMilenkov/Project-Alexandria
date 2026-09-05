using Alexandria.Common.Repositories;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Monitoring;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Alexandria.Tests.Unit.Monitoring;

public class JobOutcomeTrackerTests
{
    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();
    private readonly IServiceScopeFactory _scopeFactory = Substitute.For<IServiceScopeFactory>();
    private readonly JobOutcomeTracker _sut;

    public JobOutcomeTrackerTests()
    {
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IJobRepository)).Returns(_jobs);
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);
        _scopeFactory.CreateScope().Returns(scope);
        _sut = new JobOutcomeTracker(_scopeFactory);
    }

    [Fact]
    public async Task get_counts_since_delegates_to_job_repository_verbatim()
    {
        var since = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var ct = TestContext.Current.CancellationToken;
        _jobs.GetOutcomeCountsSinceAsync(JobType.MetadataEnrichment, since, Arg.Any<CancellationToken>())
            .Returns((3, 20));

        var result = await _sut.GetCountsSinceAsync(JobType.MetadataEnrichment, since, ct);

        result.Should().Be((3, 20));
        await _jobs.Received(1).GetOutcomeCountsSinceAsync(JobType.MetadataEnrichment, since, ct);
    }

    [Fact]
    public async Task get_counts_since_creates_a_fresh_scope_per_call()
    {
        var ct = TestContext.Current.CancellationToken;

        await _sut.GetCountsSinceAsync(JobType.Transpilation, DateTime.UtcNow, ct);
        await _sut.GetCountsSinceAsync(JobType.Transpilation, DateTime.UtcNow, ct);

        _scopeFactory.Received(2).CreateScope();
    }

    [Fact]
    public async Task get_counts_since_passes_zero_counts_through()
    {
        var since = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        _jobs.GetOutcomeCountsSinceAsync(Arg.Any<JobType>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns((0, 0));

        var result = await _sut.GetCountsSinceAsync(JobType.LyricsFetch, since, TestContext.Current.CancellationToken);

        result.Should().Be((0, 0));
    }
}