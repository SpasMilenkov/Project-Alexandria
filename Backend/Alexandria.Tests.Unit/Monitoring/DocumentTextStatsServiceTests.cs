using Alexandria.Common.Config;
using Alexandria.Common.Helpers;
using Alexandria.Common.Repositories;
using Alexandria.Services.Monitoring;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.Monitoring;

public class DocumentTextStatsServiceTests
{
    private readonly IDocumentTextRepository _repo = Substitute.For<IDocumentTextRepository>();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DocumentTextStatsService CreateSut(int maxAttempts)
        => new(_repo, Options.Create(new DocumentTextSweepConfig { MaxAttempts = maxAttempts }));

    [Fact]
    public async Task Overview_reportsTheLimitTheSweepApplies_evenWhenConfiguredBelowOne()
    {
        var viewer = Guid.NewGuid();

        var overview = await CreateSut(maxAttempts: 0).GetOverviewAsync(viewer, Ct);

        overview.MaxAttempts.Should().Be(1);
        await _repo.Received(1).GetRecentFailuresAsync(
            viewer, DocumentTextFileTypes.Supported.Types, 1, Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Overview_and_retry_coverOnlyTheTypesTheSweepReads()
    {
        var sut = CreateSut(maxAttempts: 3);

        await sut.GetOverviewAsync(Guid.NewGuid(), Ct);
        await sut.RetryFailedAsync(Ct);

        await _repo.Received(1).GetStatusCountsAsync(DocumentTextFileTypes.Supported.Types, Arg.Any<CancellationToken>());
        await _repo.Received(1).ResetFailedAsync(DocumentTextFileTypes.Supported.Types, Arg.Any<CancellationToken>());
    }
}
