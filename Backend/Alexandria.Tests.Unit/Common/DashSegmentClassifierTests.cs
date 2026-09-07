using Alexandria.Common;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Common;

public class DashSegmentClassifierTests
{
    [Theory]
    [InlineData("chunk-stream0-00001.m4s", 0)]
    [InlineData("chunk-stream12-3.m4s", 12)]
    [InlineData("init-stream2.m4s", 2)]
    [InlineData("dash/chunk-stream1-00007.m4s", 1)]
    [InlineData("v/abc/h264/dash/init-stream0.m4s", 0)]
    [InlineData("CHUNK-STREAM3-9.M4S", 3)]
    public void lane_files_resolve_to_stream_index(string key, int expected)
    {
        DashSegmentClassifier.TryGetLaneIndex(key, out var lane).Should().BeTrue();
        lane.Should().Be(expected);
    }

    [Theory]
    [InlineData("dash/manifest.mpd")]
    [InlineData("manifest.mpd")]
    [InlineData("thumbnails/abc123")]
    [InlineData("stream5.m4s")]
    [InlineData("chunk-stream-1.m4s")]
    [InlineData("chunk-fragment0-1.m4s")]
    public void shared_and_foreign_files_resolve_to_nothing(string key)
    {
        DashSegmentClassifier.TryGetLaneIndex(key, out _).Should().BeFalse();
    }
}