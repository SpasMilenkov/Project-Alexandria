using Alexandria.Services.Streaming;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Streaming;

public class FfmpegProgressTests
{
    private static readonly TimeSpan TenSeconds = TimeSpan.FromSeconds(10);

    private static FfmpegProgress After(params string[] positions)
    {
        var progress = new FfmpegProgress();

        foreach (var position in positions)
        {
            progress.Read("frame=10");
            progress.Read($"out_time_us={position}");
            progress.Read("out_time=00:00:01.000000");
            progress.Read("progress=continue");
        }

        return progress;
    }

    [Fact]
    public void Percent_severalBlocks_usesTheLatestPosition()
    {
        After("2000000", "4500000").Percent(TenSeconds).Should().Be(45);
    }

    [Fact]
    public void Percent_noPositionYet_isUnknown()
    {
        After("N/A").Percent(TenSeconds).Should().BeNull();
    }

    [Fact]
    public void Percent_nothingRead_isUnknown()
    {
        new FfmpegProgress().Percent(TenSeconds).Should().BeNull();
    }

    [Fact]
    public void Percent_unreadableLaterPosition_keepsTheLastGoodOne()
    {
        After("3000000", "N/A").Percent(TenSeconds).Should().Be(30);
    }

    // Publishing is still ahead when ffmpeg finishes, and audio can run past the container's length.
    [Theory]
    [InlineData("10000000")]
    [InlineData("12000000")]
    public void Percent_atOrPastTheEnd_staysBelowFinished(string position)
    {
        After(position).Percent(TenSeconds).Should().Be(99);
    }

    [Fact]
    public void Percent_unknownDuration_isUnknown()
    {
        After("2000000").Percent(TimeSpan.Zero).Should().BeNull();
    }

    [Fact]
    public void Arguments_sendProgressToStandardOutput()
    {
        FfmpegProgress.Arguments.Should().Be("-progress pipe:1 -stats_period 5");
    }
}
