using System.Text;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Exceptions.Policies;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Common.Policies;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Storage.Policies;
using AwesomeAssertions;
using NSubstitute;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alexandria.Tests.Unit.AutoTagging;

public class AutoTagTriggerTests
{
    private static readonly Guid FileId = Guid.Parse("aaaa0000-0000-0000-0000-000000000010");
    private const string AudioMime = "audio/mpeg";

    [Fact]
    public async Task QueueAutoTagAsync_policy_enabled_analysis_disabled_refuses_without_publish()
    {
        var (outbox, unitOfWork) = Create();
        var sut = new JobQueue(Substitute.For<ITranspilationJobService>(), Substitute.For<IPreviewService>(),
            outbox, unitOfWork, Substitute.For<ILogger<JobQueue>>(),
            Options.Create(new ProcessingCapabilities { Autotagging = true, AudioAnalysis = false }));

        var act = () => sut.QueueAutoTagAsync(FileId, new AutoTagParameters(TagSource.Auto), AudioMime,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<AutoTaggingDisabledException>();
        await NothingEnqueued(outbox);
    }

    [Fact]
    public async Task QueueIfNeededAsync_throws_when_flag_is_off()
    {
        var (outbox, unitOfWork) = Create();

        var act = () => AutoTagTrigger.QueueIfNeededAsync(outbox, unitOfWork, false, FileId, AudioMime,
            ct: TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<AutoTaggingDisabledException>();
        await NothingEnqueued(outbox);
    }

    [Fact]
    public async Task QueueIfNeededAsync_skips_when_successful_enrichment_exists()
    {
        var (outbox, unitOfWork) = Create();
        unitOfWork.FileEnrichments.HasSuccessfulAutoTagEnrichmentAsync(FileId, Arg.Any<CancellationToken>())
            .Returns(true);

        var queued = await AutoTagTrigger.QueueIfNeededAsync(outbox, unitOfWork, true, FileId, AudioMime,
            ct: TestContext.Current.CancellationToken);

        queued.Should().BeFalse();
        await NothingEnqueued(outbox);
    }

    [Fact]
    public async Task QueueIfNeededAsync_skips_when_an_attempt_is_already_in_flight()
    {
        var (outbox, unitOfWork) = Create();
        unitOfWork.EssentiaBatchFiles.HasAttemptInFlightAsync(FileId, Arg.Any<CancellationToken>())
            .Returns(true);

        var queued = await AutoTagTrigger.QueueIfNeededAsync(outbox, unitOfWork, true, FileId, AudioMime,
            ct: TestContext.Current.CancellationToken);

        queued.Should().BeFalse();
        await NothingEnqueued(outbox);
    }

    [Fact]
    public async Task QueueIfNeededAsync_publishes_when_no_successful_enrichment()
    {
        var (outbox, unitOfWork) = Create();
        unitOfWork.FileEnrichments.HasSuccessfulAutoTagEnrichmentAsync(FileId, Arg.Any<CancellationToken>())
            .Returns(false);

        var queued = await AutoTagTrigger.QueueIfNeededAsync(outbox, unitOfWork, true, FileId, AudioMime,
            ct: TestContext.Current.CancellationToken);

        queued.Should().BeTrue();
        await TriggerEnqueued(outbox);
    }

    [Fact]
    public async Task QueueIfNeededAsync_publishes_when_only_failure_rows_exist()
    {
        var (outbox, unitOfWork) = Create();
        unitOfWork.FileEnrichments.HasSuccessfulAutoTagEnrichmentAsync(FileId, Arg.Any<CancellationToken>())
            .Returns(false);

        var queued = await AutoTagTrigger.QueueIfNeededAsync(outbox, unitOfWork, true, FileId, AudioMime,
            ct: TestContext.Current.CancellationToken);

        queued.Should().BeTrue();
        await TriggerEnqueued(outbox);
    }

    [Fact]
    public async Task QueueIfNeededAsync_enqueues_inside_a_transaction()
    {
        var (outbox, unitOfWork) = Create();

        await AutoTagTrigger.QueueIfNeededAsync(outbox, unitOfWork, true, FileId, AudioMime,
            ct: TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>());
            outbox.EnqueueAsync(AutoTagTrigger.EnrichRoutingKey, Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
            unitOfWork.CommitAsync(Arg.Any<CancellationToken>());
        });
    }

    [Theory]
    [InlineData("video/mp4")]
    [InlineData("application/pdf")]
    [InlineData("")]
    [InlineData(null)]
    public async Task QueueIfNeededAsync_skips_unsupported_mime_type_without_publish(string? mimeType)
    {
        var (outbox, unitOfWork) = Create();

        var queued = await AutoTagTrigger.QueueIfNeededAsync(outbox, unitOfWork, true, FileId, mimeType!,
            ct: TestContext.Current.CancellationToken);

        queued.Should().BeFalse();
        await NothingEnqueued(outbox);
        await unitOfWork.FileEnrichments
            .DidNotReceive()
            .HasSuccessfulAutoTagEnrichmentAsync(FileId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueueIfNeededAsync_supports_application_ogg()
    {
        var (outbox, unitOfWork) = Create();
        unitOfWork.FileEnrichments.HasSuccessfulAutoTagEnrichmentAsync(FileId, Arg.Any<CancellationToken>())
            .Returns(false);

        var queued = await AutoTagTrigger.QueueIfNeededAsync(outbox, unitOfWork, true, FileId, "application/ogg",
            ct: TestContext.Current.CancellationToken);

        queued.Should().BeTrue();
        await outbox.Received(1).EnqueueAsync(AutoTagTrigger.EnrichRoutingKey, Arg.Any<byte[]>(),
            Arg.Any<CancellationToken>());
    }

    private static Task NothingEnqueued(IOutbox outbox)
        => outbox.DidNotReceiveWithAnyArgs().EnqueueAsync(default!, default!, TestContext.Current.CancellationToken);

    private static Task TriggerEnqueued(IOutbox outbox)
        => outbox.Received(1).EnqueueAsync(
            AutoTagTrigger.EnrichRoutingKey,
            Arg.Is<byte[]>(body => Encoding.UTF8.GetString(body) == FileId.ToString()),
            Arg.Any<CancellationToken>());

    private static (IOutbox Outbox, IUnitOfWork UnitOfWork) Create()
    {
        var outbox = Substitute.For<IOutbox>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.FileEnrichments.Returns(Substitute.For<IFileEnrichmentRepository>());
        unitOfWork.EssentiaBatchFiles.Returns(Substitute.For<IEssentiaBatchFileRepository>());
        return (outbox, unitOfWork);
    }
}
