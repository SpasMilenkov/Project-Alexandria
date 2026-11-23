using Alexandria.Common;
using Alexandria.Common.Notes;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Dto.Links;
using Alexandria.Services.Storage.Links;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Alexandria.Tests.Unit.Notes;

public class NoteLinkScanWorkerTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly INoteLinkRepository _noteLinks = Substitute.For<INoteLinkRepository>();
    private readonly INoteLinkIndexService _index = Substitute.For<INoteLinkIndexService>();

    public NoteLinkScanWorkerTests()
    {
        _unitOfWork.NoteLinks.Returns(_noteLinks);
    }

    private static NoteLinkScanCandidate NewCandidate() => new()
    {
        ContentObjectId = Guid.NewGuid(),
        VersionId = Guid.NewGuid(),
        OwnerId = Guid.NewGuid(),
    };

    private void GivenCandidates(params NoteLinkScanCandidate[] candidates)
    {
        _noteLinks.GetScanCandidatesAsync(NoteLinkExtractor.CurrentVersion, TextContentLimits.MaxBytes,
                NoteLinkScanWorker.BatchSize, Arg.Any<CancellationToken>())
            .Returns(candidates);
    }

    private NoteLinkScanWorker CreateSut()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _unitOfWork);
        services.AddScoped(_ => _index);

        return new NoteLinkScanWorker(services.BuildServiceProvider(), Substitute.For<ILogger<NoteLinkScanWorker>>());
    }

    [Fact]
    public async Task Sweep_indexesEveryCandidateAndReportsHowManyWereListed()
    {
        var first = NewCandidate();
        var second = NewCandidate();
        GivenCandidates(first, second);

        var listed = await CreateSut().SweepAsync(TestContext.Current.CancellationToken);

        listed.Should().Be(2);
        await _index.Received(1).IndexCandidateAsync(first, Arg.Any<CancellationToken>());
        await _index.Received(1).IndexCandidateAsync(second, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sweep_oneNoteFails_carriesOnWithTheRest()
    {
        var broken = NewCandidate();
        var fine = NewCandidate();
        GivenCandidates(broken, fine);
        _index.IndexCandidateAsync(broken, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("odd note"));

        await CreateSut().SweepAsync(TestContext.Current.CancellationToken);

        await _index.Received(1).IndexCandidateAsync(fine, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sweep_severalFailuresInARow_stopsTheBatch()
    {
        var candidates = Enumerable.Range(0, NoteLinkScanWorker.MaxFailuresInARow + 2).Select(_ => NewCandidate())
            .ToArray();
        GivenCandidates(candidates);
        _index.IndexCandidateAsync(Arg.Any<NoteLinkScanCandidate>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("storage unreachable"));

        await CreateSut().SweepAsync(TestContext.Current.CancellationToken);

        await _index.Received(NoteLinkScanWorker.MaxFailuresInARow)
            .IndexCandidateAsync(Arg.Any<NoteLinkScanCandidate>(), Arg.Any<CancellationToken>());
    }
}
