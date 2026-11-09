using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Helpers;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Dto.Files;
using Alexandria.Workers.Document;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.Documents;

public class DocumentMetadataSweepWorkerTests
{
    private const string Pdf = "application/pdf";

    private readonly IDocumentMetadataRepository _repository = Substitute.For<IDocumentMetadataRepository>();
    private readonly IContentStorageService _storage = Substitute.For<IContentStorageService>();
    private readonly IDocumentMetadataService _documentMetadata = Substitute.For<IDocumentMetadataService>();

    private DocumentMetadataSweepWorker CreateSut()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.DocumentMetadata.Returns(_repository);

        var services = new ServiceCollection();
        services.AddSingleton(unitOfWork);
        services.AddSingleton(_storage);
        services.AddSingleton(_documentMetadata);

        return new DocumentMetadataSweepWorker(
            NullLogger<DocumentMetadataSweepWorker>.Instance,
            Options.Create(new DocumentMetadataSweepConfig { BatchSize = 2 }),
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
    }

    private void Candidates(params DocumentMetadataCandidate[] candidates)
    {
        _repository.GetExtractionCandidatesAsync(
                DocumentMetadataFileTypes.Supported.Types, DocumentMetadataExtractor.CurrentVersion, 2,
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(candidates);
    }

    [Fact]
    public async Task SweepBatch_everyCandidate_isDownloadedAndStoredWithItsType()
    {
        var pdf = new DocumentMetadataCandidate(Guid.NewGuid(), Pdf);
        var odt = new DocumentMetadataCandidate(Guid.NewGuid(), "application/vnd.oasis.opendocument.text");
        Candidates(pdf, odt);

        var stored = await CreateSut().SweepBatchAsync(TestContext.Current.CancellationToken);

        stored.Should().Be(2);
        await _storage.Received(1).StreamFile(pdf.VersionId, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await _documentMetadata.Received(1).ExtractAndStoreAsync(
            pdf.VersionId, Pdf, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _documentMetadata.Received(1).ExtractAndStoreAsync(
            odt.VersionId, odt.MimeType, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SweepBatch_nothingToDo_returnsZeroWithoutTouchingStorage()
    {
        Candidates();

        var stored = await CreateSut().SweepBatchAsync(TestContext.Current.CancellationToken);

        stored.Should().Be(0);
        await _storage.DidNotReceive().StreamFile(Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SweepBatch_oneFailedVersion_doesNotStopTheRest()
    {
        var broken = new DocumentMetadataCandidate(Guid.NewGuid(), Pdf);
        var healthy = new DocumentMetadataCandidate(Guid.NewGuid(), Pdf);
        Candidates(broken, healthy);
        _documentMetadata.ExtractAndStoreAsync(broken.VersionId, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DocumentMetadata>(new InvalidOperationException("damaged")));

        var stored = await CreateSut().SweepBatchAsync(TestContext.Current.CancellationToken);

        stored.Should().Be(1);
        await _documentMetadata.Received(1).ExtractAndStoreAsync(
            healthy.VersionId, Pdf, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SweepBatch_failedVersion_isExcludedFromTheNextBatch()
    {
        var broken = new DocumentMetadataCandidate(Guid.NewGuid(), Pdf);
        Candidates(broken);
        _documentMetadata.ExtractAndStoreAsync(broken.VersionId, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DocumentMetadata>(new InvalidOperationException("damaged")));
        var sut = CreateSut();

        await sut.SweepBatchAsync(TestContext.Current.CancellationToken);
        await sut.SweepBatchAsync(TestContext.Current.CancellationToken);

        await _repository.Received().GetExtractionCandidatesAsync(
            Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Is<IReadOnlyCollection<Guid>>(excluded => excluded.Contains(broken.VersionId)),
            Arg.Any<CancellationToken>());
    }
}
