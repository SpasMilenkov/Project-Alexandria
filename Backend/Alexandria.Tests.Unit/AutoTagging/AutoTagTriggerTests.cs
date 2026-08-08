using System.Text;
using Alexandria.Common;
using Alexandria.Common.Exceptions.Policies;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Services.Storage.Policies;
using AwesomeAssertions;
using NSubstitute;

namespace Alexandria.Tests.Unit.AutoTagging;

public class AutoTagTriggerTests
{
    private static readonly Guid FileId = Guid.Parse("aaaa0000-0000-0000-0000-000000000010");
    private const string AudioMime = "audio/mpeg";

    [Fact]
    public async Task QueueIfNeededAsync_throws_when_flag_is_off()
    {
        var (publisher, unitOfWork) = Create();

        var act = () => AutoTagTrigger.QueueIfNeededAsync(publisher, unitOfWork, false, FileId, AudioMime);

        await act.Should().ThrowAsync<AutoTaggingDisabledException>();
        await publisher.DidNotReceive().PublishAsync(Arg.Any<byte[]>(), Arg.Any<string>());
    }

    [Fact]
    public async Task QueueIfNeededAsync_skips_when_successful_enrichment_exists()
    {
        var (publisher, unitOfWork) = Create();
        unitOfWork.FileEnrichments.HasSuccessfulAutoTagEnrichmentAsync(FileId, Arg.Any<CancellationToken>())
            .Returns(true);

        var queued = await AutoTagTrigger.QueueIfNeededAsync(publisher, unitOfWork, true, FileId, AudioMime);

        queued.Should().BeFalse();
        await publisher.DidNotReceive().PublishAsync(Arg.Any<byte[]>(), Arg.Any<string>());
    }

    [Fact]
    public async Task QueueIfNeededAsync_publishes_when_no_successful_enrichment()
    {
        var (publisher, unitOfWork) = Create();
        unitOfWork.FileEnrichments.HasSuccessfulAutoTagEnrichmentAsync(FileId, Arg.Any<CancellationToken>())
            .Returns(false);

        var queued = await AutoTagTrigger.QueueIfNeededAsync(publisher, unitOfWork, true, FileId, AudioMime);

        queued.Should().BeTrue();
        await publisher.Received(1).PublishAsync(
            Arg.Is<byte[]>(body => Encoding.UTF8.GetString(body) == FileId.ToString()),
            AutoTagTrigger.EnrichRoutingKey);
    }

    [Fact]
    public async Task QueueIfNeededAsync_publishes_when_only_failure_rows_exist()
    {
        var (publisher, unitOfWork) = Create();
        unitOfWork.FileEnrichments.HasSuccessfulAutoTagEnrichmentAsync(FileId, Arg.Any<CancellationToken>())
            .Returns(false);

        var queued = await AutoTagTrigger.QueueIfNeededAsync(publisher, unitOfWork, true, FileId, AudioMime);

        queued.Should().BeTrue();
        await publisher.Received(1).PublishAsync(
            Arg.Is<byte[]>(body => Encoding.UTF8.GetString(body) == FileId.ToString()),
            AutoTagTrigger.EnrichRoutingKey);
    }

    [Theory]
    [InlineData("video/mp4")]
    [InlineData("application/pdf")]
    [InlineData("")]
    [InlineData(null)]
    public async Task QueueIfNeededAsync_skips_unsupported_mime_type_without_publish(string? mimeType)
    {
        var (publisher, unitOfWork) = Create();

        var queued = await AutoTagTrigger.QueueIfNeededAsync(publisher, unitOfWork, true, FileId, mimeType!);

        queued.Should().BeFalse();
        await publisher.DidNotReceive().PublishAsync(Arg.Any<byte[]>(), Arg.Any<string>());
        await unitOfWork.FileEnrichments
            .DidNotReceive()
            .HasSuccessfulAutoTagEnrichmentAsync(FileId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueueIfNeededAsync_supports_application_ogg()
    {
        var (publisher, unitOfWork) = Create();
        unitOfWork.FileEnrichments.HasSuccessfulAutoTagEnrichmentAsync(FileId, Arg.Any<CancellationToken>())
            .Returns(false);

        var queued = await AutoTagTrigger.QueueIfNeededAsync(publisher, unitOfWork, true, FileId, "application/ogg");

        queued.Should().BeTrue();
        await publisher.Received(1).PublishAsync(Arg.Any<byte[]>(), AutoTagTrigger.EnrichRoutingKey);
    }

    private static (IPublisherService Publisher, IUnitOfWork UnitOfWork) Create()
    {
        var publisher = Substitute.For<IPublisherService>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.FileEnrichments.Returns(Substitute.For<IFileEnrichmentRepository>());
        return (publisher, unitOfWork);
    }
}