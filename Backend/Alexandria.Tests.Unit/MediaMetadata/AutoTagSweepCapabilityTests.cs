using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Services.Storage.AutoTagging;
using Alexandria.Workers.MediaMetadata.Queueing;
using Alexandria.Workers.MediaMetadata.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.MediaMetadata;

public class AutoTagSweepCapabilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SweepAsync_audio_analysis_controls_failed_retry_and_tag_backstop(bool enabled)
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var files = Substitute.For<IEssentiaBatchFileRepository>();
        unitOfWork.EssentiaBatchFiles.Returns(files);
        var fileId = Guid.NewGuid();
        files.GetAutoTagBackstopCandidatesAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Guid>());
        files.GetAutoTagRetryCandidatesAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new[] { fileId });
        var outbox = Substitute.For<IOutbox>();
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(IUnitOfWork)).Returns(unitOfWork);
        provider.GetService(typeof(IOutbox)).Returns(outbox);
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(provider);
        var sut = new AutoTagSweepWorker(Substitute.For<ILogger<AutoTagSweepWorker>>(),
            Options.Create(new AutoTaggingOptions()), provider, new AutoTagQueueService(),
            Options.Create(new ProcessingCapabilities { Autotagging = true, AudioAnalysis = enabled }));

        await sut.SweepAsync(scope, TestContext.Current.CancellationToken);

        if (enabled)
        {
            await files.Received(1).GetAutoTagBackstopCandidatesAsync(TestContext.Current.CancellationToken);
            await outbox.Received(1).EnqueueAsync(
                "media-metadata.enrich", Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
            await unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        }
        else
        {
            await files.DidNotReceiveWithAnyArgs().GetAutoTagBackstopCandidatesAsync(default);
            await files.DidNotReceiveWithAnyArgs().GetAutoTagRetryCandidatesAsync(default, default, default);
            await outbox.DidNotReceiveWithAnyArgs().EnqueueAsync(default!, default!, TestContext.Current.CancellationToken);
        }
    }
}
