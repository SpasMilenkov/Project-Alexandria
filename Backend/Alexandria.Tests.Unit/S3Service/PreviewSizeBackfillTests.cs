using System.Net;
using Alexandria.Common.Config;
using Alexandria.Common.Repositories;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Storage.Cleanup;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.S3Service;

public class PreviewSizeBackfillTests
{
    private readonly IPreviewRepository _previews = Substitute.For<IPreviewRepository>();
    private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();
    private readonly PreviewSizeBackfillService _sut;

    public PreviewSizeBackfillTests()
    {
        var config = Options.Create(new S3Config { PreviewBucket = "previews" });
        _sut = new PreviewSizeBackfillService(
            _previews, _s3, config, Substitute.For<ILogger<PreviewSizeBackfillService>>());
        _previews.TryBackfillSizeAsync(Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    private static Preview MissingPreview(Guid? id = null, string objectKey = "thumbnails/abc") => new()
    {
        Id = id ?? Guid.NewGuid(),
        MimeType = "image/png",
        Size = 0L,
        Kind = PreviewKind.Thumbnail,
        CreatedAt = DateTime.UtcNow,
        VersionId = Guid.NewGuid(),
        ObjectKey = objectKey,
    };

    private void MissingBatches(params IReadOnlyList<Preview>[] batches)
    {
        var ct = TestContext.Current.CancellationToken;
        if (batches.Length == 0)
        {
            _previews.GetMissingSizesAsync(Arg.Any<int>(), ct)
                .Returns(new List<Preview>());
            return;
        }

        _previews.GetMissingSizesAsync(Arg.Any<int>(), ct).Returns(batches[0], batches[1..]);
    }

    private void MetadataSizes(Dictionary<string, long> sizes)
    {
        var ct = TestContext.Current.CancellationToken;
        _s3.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), ct)
            .Returns(call => new GetObjectMetadataResponse
            {
                ContentLength = sizes[call.ArgAt<string>(1)],
            });
    }

    [Fact]
    public async Task backfill_writes_sizes_from_object_metadata()
    {
        var ct = TestContext.Current.CancellationToken;
        var first = MissingPreview(objectKey: "thumbnails/a");
        var second = MissingPreview(objectKey: "thumbnails/b");
        MissingBatches([first, second], []);
        MetadataSizes(new Dictionary<string, long> { ["thumbnails/a"] = 100L, ["thumbnails/b"] = 200L });

        var result = await _sut.BackfillAsync(ct);

        result.Should().Be(new PreviewSizeBackfillResult(
            Scanned: 2, Backfilled: 2, Missing: 0, Skipped: 0, Failed: 0));
        await _previews.Received(1).TryBackfillSizeAsync(first.Id, 100L, ct);
        await _previews.Received(1).TryBackfillSizeAsync(second.Id, 200L, ct);
    }

    [Fact]
    public async Task backfill_skips_missing_objects_without_writing()
    {
        var ct = TestContext.Current.CancellationToken;
        var gone = MissingPreview(objectKey: "thumbnails/gone");
        var present = MissingPreview(objectKey: "thumbnails/here");
        MissingBatches([gone, present], []);
        _s3.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), ct)
            .Returns(
                _ => throw new AmazonS3Exception("not found", ErrorType.Sender, "NoSuchKey", "req",
                    HttpStatusCode.NotFound),
                _ => new GetObjectMetadataResponse { ContentLength = 50L });

        var result = await _sut.BackfillAsync(ct);

        result.Missing.Should().Be(1);
        result.Backfilled.Should().Be(1);
        await _previews.DidNotReceive().TryBackfillSizeAsync(gone.Id, Arg.Any<long>(), ct);
        await _previews.Received(1).TryBackfillSizeAsync(present.Id, 50L, ct);
    }

    [Fact]
    public async Task backfill_skips_preview_without_resolvable_key()
    {
        var ct = TestContext.Current.CancellationToken;
        MissingBatches([MissingPreview(objectKey: string.Empty)], []);

        var result = await _sut.BackfillAsync(ct);

        result.Should().Be(new PreviewSizeBackfillResult(
            Scanned: 1, Backfilled: 0, Missing: 0, Skipped: 1, Failed: 0));
        await _s3.DidNotReceive().GetObjectMetadataAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task backfill_continues_after_storage_error()
    {
        var ct = TestContext.Current.CancellationToken;
        var broken = MissingPreview(objectKey: "thumbnails/broken");
        var fine = MissingPreview(objectKey: "thumbnails/fine");
        MissingBatches([broken, fine], []);
        _s3.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), ct)
            .Returns(
                _ => throw new AmazonS3Exception("boom"),
                _ => new GetObjectMetadataResponse { ContentLength = 70L });

        var result = await _sut.BackfillAsync(ct);

        result.Failed.Should().Be(1);
        result.Backfilled.Should().Be(1);
        await _previews.Received(1).TryBackfillSizeAsync(fine.Id, 70L, ct);
    }

    [Fact]
    public async Task backfill_counts_lost_race_as_neither_backfilled_nor_failed()
    {
        var ct = TestContext.Current.CancellationToken;
        MissingBatches([MissingPreview()], []);
        MetadataSizes(new Dictionary<string, long> { ["thumbnails/abc"] = 10L });
        _previews.TryBackfillSizeAsync(Arg.Any<Guid>(), Arg.Any<long>(), ct).Returns(false);

        var result = await _sut.BackfillAsync(ct);

        result.Should().Be(new PreviewSizeBackfillResult(
            Scanned: 1, Backfilled: 0, Missing: 0, Skipped: 0, Failed: 0));
    }

    [Fact]
    public async Task backfill_with_no_missing_rows_touches_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        MissingBatches([]);

        var result = await _sut.BackfillAsync(ct);

        result.Should().Be(new PreviewSizeBackfillResult(
            Scanned: 0, Backfilled: 0, Missing: 0, Skipped: 0, Failed: 0));
        await _s3.DidNotReceive().GetObjectMetadataAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}