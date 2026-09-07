using Alexandria.Common.Config;
using Alexandria.Common.Repositories;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Storage.Cleanup;
using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.S3Service;

public class RepresentationSizeBackfillTests
{
    private readonly IStreamingRepresentationRepository _representations =
        Substitute.For<IStreamingRepresentationRepository>();

    private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();
    private readonly RepresentationSizeBackfillService _sut;

    public RepresentationSizeBackfillTests()
    {
        var config = Options.Create(new S3Config { StreamingBucket = "streaming" });
        _sut = new RepresentationSizeBackfillService(
            _representations, _s3, config, Substitute.For<ILogger<RepresentationSizeBackfillService>>());
        _representations.TryBackfillSizeAsync(Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    private static StreamingRepresentation Rep(
        Guid jobId, string prefix, int bitrate, int? width = null, int? height = null) => new()
    {
        Id = Guid.NewGuid(),
        TranspilationId = jobId,
        Codec = StreamCodec.H264,
        BitrateKbps = bitrate,
        Width = width,
        Height = height,
        Size = 0L,
        Status = RepresentationStatus.Ready,
        CreatedAt = DateTime.UtcNow,
        Job = new TranspilationJob { Id = jobId, SegmentPrefix = prefix },
    };

    private void MissingBatches(params IReadOnlyList<StreamingRepresentation>[] batches)
    {
        var ct = TestContext.Current.CancellationToken;
        if (batches.Length == 0)
        {
            _representations.GetMissingSizesAsync(Arg.Any<int>(), ct)
                .Returns(new List<StreamingRepresentation>());
            return;
        }

        _representations.GetMissingSizesAsync(Arg.Any<int>(), ct).Returns(batches[0], batches[1..]);
    }

    private void ListObjects(Dictionary<string, List<S3Object>> byPrefix)
    {
        var ct = TestContext.Current.CancellationToken;
        _s3.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), ct)
            .Returns(call => new ListObjectsV2Response
            {
                S3Objects = byPrefix.TryGetValue(call.Arg<ListObjectsV2Request>().Prefix ?? string.Empty,
                    out var objects)
                    ? objects
                    : [],
                IsTruncated = false,
            });
    }

    private static S3Object Obj(string key, long size) => new() { Key = key, Size = size };

    [Fact]
    public async Task backfill_attributes_lane_bytes_by_stream_index_in_bitrate_order()
    {
        var ct = TestContext.Current.CancellationToken;
        var jobId = Guid.NewGuid();
        // Created out of order on purpose: bitrate ordering must reconstruct lanes.
        var high = Rep(jobId, "v/abc/h264", 2500, 1280, 720);
        var low = Rep(jobId, "v/abc/h264", 800, 640, 360);
        MissingBatches([high, low], []);
        ListObjects(new Dictionary<string, List<S3Object>>
        {
            ["v/abc/h264/"] =
            [
                Obj("v/abc/h264/dash/chunk-stream0-00001.m4s", 100L),
                Obj("v/abc/h264/dash/chunk-stream0-00002.m4s", 50L),
                Obj("v/abc/h264/dash/init-stream1.m4s", 300L),
                Obj("v/abc/h264/dash/manifest.mpd", 10L),
                Obj("v/abc/h264/dash/chunk-stream5-00001.m4s", 999L),
            ],
        });

        var result = await _sut.BackfillAsync(ct);

        result.Should().Be(new RepresentationSizeBackfillResult(
            Scanned: 2, Backfilled: 2, Jobs: 1, Unmatched: 0, Failed: 0));
        await _representations.Received(1).TryBackfillSizeAsync(low.Id, 150L, ct);
        await _representations.Received(1).TryBackfillSizeAsync(high.Id, 300L, ct);
    }

    [Fact]
    public async Task backfill_leaves_lanes_without_bytes_unmatched()
    {
        var ct = TestContext.Current.CancellationToken;
        var row = Rep(Guid.NewGuid(), "v/empty/h264", 800);
        MissingBatches([row], []);
        ListObjects(new Dictionary<string, List<S3Object>>());

        var result = await _sut.BackfillAsync(ct);

        result.Should().Be(new RepresentationSizeBackfillResult(
            Scanned: 1, Backfilled: 0, Jobs: 1, Unmatched: 1, Failed: 0));
        await _representations.DidNotReceive().TryBackfillSizeAsync(
            Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task backfill_continues_after_list_failure()
    {
        var ct = TestContext.Current.CancellationToken;
        var brokenJob = Guid.NewGuid();
        var broken = Rep(brokenJob, "v/broken/h264", 800);
        var fineJob = Guid.NewGuid();
        var fine = Rep(fineJob, "v/fine/h264", 800);
        MissingBatches([broken, fine], []);
        _s3.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), ct)
            .Returns(
                _ => throw new AmazonS3Exception("boom"),
                _ => new ListObjectsV2Response
                {
                    S3Objects = [Obj("v/fine/h264/dash/chunk-stream0-1.m4s", 70L)],
                    IsTruncated = false,
                });

        var result = await _sut.BackfillAsync(ct);

        result.Failed.Should().Be(1);
        result.Backfilled.Should().Be(1);
        await _representations.Received(1).TryBackfillSizeAsync(fine.Id, 70L, ct);
    }

    [Fact]
    public async Task backfill_counts_lost_race_as_neither_backfilled_nor_failed()
    {
        var ct = TestContext.Current.CancellationToken;
        MissingBatches([Rep(Guid.NewGuid(), "v/race/h264", 800)], []);
        ListObjects(new Dictionary<string, List<S3Object>>
        {
            ["v/race/h264/"] = [Obj("v/race/h264/dash/chunk-stream0-1.m4s", 10L)],
        });
        _representations.TryBackfillSizeAsync(Arg.Any<Guid>(), Arg.Any<long>(), ct).Returns(false);

        var result = await _sut.BackfillAsync(ct);

        result.Should().Be(new RepresentationSizeBackfillResult(
            Scanned: 1, Backfilled: 0, Jobs: 1, Unmatched: 0, Failed: 0));
    }

    [Fact]
    public async Task backfill_with_no_missing_rows_touches_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        MissingBatches([]);

        var result = await _sut.BackfillAsync(ct);

        result.Should().Be(new RepresentationSizeBackfillResult(
            Scanned: 0, Backfilled: 0, Jobs: 0, Unmatched: 0, Failed: 0));
        await _s3.DidNotReceive().ListObjectsV2Async(
            Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task backfill_without_configured_bucket_touches_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = new RepresentationSizeBackfillService(
            _representations, _s3, Options.Create(new S3Config { StreamingBucket = string.Empty }),
            Substitute.For<ILogger<RepresentationSizeBackfillService>>());

        var result = await sut.BackfillAsync(ct);

        result.Should().Be(new RepresentationSizeBackfillResult(
            Scanned: 0, Backfilled: 0, Jobs: 0, Unmatched: 0, Failed: 0));
        await _representations.DidNotReceive().GetMissingSizesAsync(
            Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}