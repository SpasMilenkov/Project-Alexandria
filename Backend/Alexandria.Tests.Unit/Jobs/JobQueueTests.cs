using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Policies;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Storage.Policies;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.Jobs;

public class JobQueueTests
{
    [Fact]
    public async Task QueueTranspilationJobAsync_requestsThePreviewForTheVersion()
    {
        var preview = Substitute.For<IPreviewService>();
        var sut = new JobQueue(
            Substitute.For<ITranspilationJobService>(),
            preview,
            Substitute.For<IPublisherService>(),
            Substitute.For<IUnitOfWork>(),
            NullLogger<JobQueue>.Instance,
            Options.Create(new ProcessingCapabilities()));

        var versionId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await sut.QueueTranspilationJobAsync(
            versionId, fileId, userId, new TranscodeParameters([], [], false), TestContext.Current.CancellationToken);

        await preview.Received(1).GeneratePreviewAsync(
            versionId, userId, Arg.Any<PreviewKind>(), Arg.Any<CancellationToken>());
    }
}
