using Alexandria.Services.Streaming;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Streaming;

public class MediaDurationTests
{
    [Fact]
    public void Parse_secondsWithNewline_readsThem()
    {
        MediaDuration.Parse("209.400000\n").Should().Be(TimeSpan.FromSeconds(209.4));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("N/A")]
    [InlineData("0.000000")]
    [InlineData("-1")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public void Parse_noUsableLength_isUnknown(string? seconds)
    {
        MediaDuration.Parse(seconds).Should().BeNull();
    }

    [Fact]
    public void ProbeArguments_quoteTheSource()
    {
        MediaDuration.ProbeArguments("/in/my source").Should()
            .Be("-v error -show_entries format=duration -of csv=p=0 \"/in/my source\"");
    }
}
