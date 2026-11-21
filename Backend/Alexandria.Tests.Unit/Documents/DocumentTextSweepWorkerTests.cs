using System.ComponentModel;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Helpers;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Dto.Files;
using Alexandria.Workers.Document;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Alexandria.Tests.Unit.Documents;

public class DocumentTextSweepWorkerTests
{
    private const string Pdf = "application/pdf";

    private readonly IDocumentTextRepository _repository = Substitute.For<IDocumentTextRepository>();
    private readonly IOperationalEventRepository _events = Substitute.For<IOperationalEventRepository>();
    private readonly IContentStorageService _storage = Substitute.For<IContentStorageService>();
    private readonly IDocumentTextService _extractor = Substitute.For<IDocumentTextService>();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DocumentTextSweepWorker CreateSut(DocumentTextSweepConfig? config = null)
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.DocumentText.Returns(_repository);
        unitOfWork.OperationalEvents.Returns(_events);

        var services = new ServiceCollection();
        services.AddSingleton(unitOfWork);
        services.AddSingleton(_storage);
        services.AddSingleton(_extractor);

        return new DocumentTextSweepWorker(
            NullLogger<DocumentTextSweepWorker>.Instance,
            Options.Create(config ?? new DocumentTextSweepConfig { BatchSize = 3, MaxAttempts = 4 }),
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
    }

    private void Candidates(params DocumentTextCandidate[] candidates)
    {
        _repository.GetExtractionCandidatesAsync(
                Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<int>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(candidates);
    }

    private static DocumentTextCandidate Candidate(string mimeType = Pdf)
        => new(Guid.NewGuid(), Guid.NewGuid(), mimeType);

    private void Extracts(DocumentTextCandidate candidate, DocumentTextResult result)
    {
        _extractor.ExtractAsync(candidate.MimeType, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(result);
    }

    private void DownloadFails(DocumentTextCandidate candidate, Exception error)
    {
        _storage.StreamFile(candidate.VersionId, Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(error);
    }

    [Fact]
    public async Task SweepBatch_asksForSupportedTypesAtTheCurrentVersion_withTheConfiguredLimits()
    {
        Candidates();
        var before = DateTime.UtcNow;

        await CreateSut(new DocumentTextSweepConfig { BatchSize = 7, MaxAttempts = 2, RetryFailedAfterHours = 6 })
            .SweepBatchAsync(Ct);

        await _repository.Received(1).GetExtractionCandidatesAsync(
            DocumentTextFileTypes.Supported.Types,
            DocumentTextExtractor.CurrentVersion,
            Arg.Is<DateTime>(d => d <= before.AddHours(-6).AddSeconds(5) && d >= before.AddHours(-6).AddSeconds(-5)),
            2,
            7,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SweepBatch_nothingToDo_returnsZeroWithoutTouchingStorage()
    {
        Candidates();

        var processed = await CreateSut().SweepBatchAsync(Ct);

        processed.Should().Be(0);
        await _storage.DidNotReceive().StreamFile(Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SweepBatch_storesEachResult_byContent_throughItsVersion()
    {
        var candidate = Candidate();
        Candidates(candidate);
        Extracts(candidate, new DocumentTextResult(DocumentTextStatus.Extracted, "hotel invoice", IsTruncated: true));

        var processed = await CreateSut().SweepBatchAsync(Ct);

        processed.Should().Be(1);
        await _storage.Received(1).StreamFile(candidate.VersionId, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).UpsertAsync(
            Arg.Is<DocumentText>(t =>
                t.ContentObjectId == candidate.ContentObjectId
                && t.Status == DocumentTextStatus.Extracted
                && t.Content == "hotel invoice"
                && t.CharacterCount == 13
                && t.IsTruncated
                && t.ExtractorVersion == DocumentTextExtractor.CurrentVersion),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SweepBatch_lockedDocument_isStoredAsAFinalRow()
    {
        var candidate = Candidate();
        Candidates(candidate);
        Extracts(candidate, new DocumentTextResult(DocumentTextStatus.PasswordProtected, "", IsTruncated: false));

        await CreateSut().SweepBatchAsync(Ct);

        await _repository.Received(1).UpsertAsync(
            Arg.Is<DocumentText>(t => t.Status == DocumentTextStatus.PasswordProtected && t.Content == ""),
            Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().RecordFailureAsync(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().FailAttemptAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SweepBatch_countsTheAttempt_beforeReadingTheDocument()
    {
        var candidate = Candidate();
        Candidates(candidate);
        Extracts(candidate, new DocumentTextResult(DocumentTextStatus.Extracted, "text", IsTruncated: false));

        await CreateSut().SweepBatchAsync(Ct);

        Received.InOrder(() =>
        {
            _repository.BeginAttemptAsync(
                candidate.ContentObjectId, DocumentTextExtractor.CurrentVersion, Arg.Any<CancellationToken>());
            _extractor.ExtractAsync(candidate.MimeType, Arg.Any<string>(), Arg.Any<CancellationToken>());
            _repository.UpsertAsync(Arg.Any<DocumentText>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task SweepBatch_brokenDocument_isRecordedOnItsRow_andTheRestCarryOn()
    {
        var broken = Candidate();
        var healthy = Candidate("text/plain");
        Candidates(broken, healthy);
        _extractor.ExtractAsync(Pdf, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidDataException("damaged package"));
        Extracts(healthy, new DocumentTextResult(DocumentTextStatus.Extracted, "notes", IsTruncated: false));

        var processed = await CreateSut().SweepBatchAsync(Ct);

        processed.Should().Be(2);
        // The attempt was already counted, so the failure only fills in why.
        await _repository.Received(1).FailAttemptAsync(
            broken.ContentObjectId, "damaged package", Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().RecordFailureAsync(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().AbandonAttemptAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).UpsertAsync(
            Arg.Is<DocumentText>(t => t.ContentObjectId == healthy.ContentObjectId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SweepBatch_shutdownWhileReading_givesTheAttemptBack()
    {
        var candidate = Candidate();
        Candidates(candidate);
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        _extractor.ExtractAsync(Pdf, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<DocumentTextResult>(_ =>
            {
                shutdown.Cancel();
                throw new OperationCanceledException(shutdown.Token);
            });

        var sweep = () => CreateSut().SweepBatchAsync(shutdown.Token);

        await sweep.Should().ThrowAsync<OperationCanceledException>();
        await _repository.Received(1).AbandonAttemptAsync(candidate.ContentObjectId, Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().FailAttemptAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SweepBatch_oneDownloadFailing_isThatDocumentsFailure()
    {
        var missing = Candidate();
        var present = Candidate();
        Candidates(missing, present);
        DownloadFails(missing, new IOException("object not found"));
        Extracts(present, new DocumentTextResult(DocumentTextStatus.Extracted, "text", IsTruncated: false));

        var processed = await CreateSut().SweepBatchAsync(Ct);

        processed.Should().Be(2);
        await _repository.Received(1).RecordFailureAsync(
            missing.ContentObjectId, DocumentTextExtractor.CurrentVersion,
            Arg.Is<string>(s => s.Contains("object not found")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SweepBatch_everyDownloadFailing_throws_withoutChargingAnyDocument()
    {
        var first = Candidate();
        var second = Candidate();
        Candidates(first, second);
        DownloadFails(first, new HttpRequestException("connection refused"));
        DownloadFails(second, new HttpRequestException("connection refused"));

        var sweep = () => CreateSut().SweepBatchAsync(Ct);

        await sweep.Should().ThrowAsync<InvalidOperationException>().WithMessage("*connection refused*");
        await _repository.DidNotReceive().RecordFailureAsync(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().BeginAttemptAsync(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SweepBatch_missingTool_throws_withoutChargingTheDocument()
    {
        var candidate = Candidate();
        Candidates(candidate);
        _extractor.ExtractAsync(Pdf, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Win32Exception(2, "No such file or directory"));

        var sweep = () => CreateSut().SweepBatchAsync(Ct);

        await sweep.Should().ThrowAsync<Win32Exception>();
        await _repository.DidNotReceive().RecordFailureAsync(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().FailAttemptAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).AbandonAttemptAsync(candidate.ContentObjectId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_failedCycle_isReportedToMonitoringForDocumentPreviews()
    {
        _repository.GetExtractionCandidatesAsync(
                Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<int>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("database unavailable"));
        var reported = new TaskCompletionSource();
        _events.AddActiveIfAbsentAsync(Arg.Any<OperationalEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                reported.TrySetResult();
                return true;
            });
        var sut = CreateSut(new DocumentTextSweepConfig { BatchDelaySeconds = 0, IdleDelaySeconds = 60 });

        await sut.StartAsync(Ct);
        await reported.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await sut.StopAsync(Ct);

        await _events.Received(1).AddActiveIfAbsentAsync(
            Arg.Is<OperationalEvent>(e =>
                e.ServiceType == ServiceType.DocumentPreviews
                && e.Code == OperationalEventCode.WorkerCycleFailure
                && e.MetadataJson.Contains(DocumentTextSweepWorker.ServiceInstance)
                && e.MetadataJson.Contains("database unavailable")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_disabled_neverQueries()
    {
        var sut = CreateSut(new DocumentTextSweepConfig { Enabled = false, BatchDelaySeconds = 0 });

        await sut.StartAsync(Ct);
        await sut.ExecuteTask!;
        await sut.StopAsync(Ct);

        await _repository.DidNotReceive().GetExtractionCandidatesAsync(
            Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<int>(),
            Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}