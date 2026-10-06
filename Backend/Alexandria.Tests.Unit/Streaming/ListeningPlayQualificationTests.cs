using Alexandria.Common;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Streaming;

public class ListeningPlayQualificationTests
{
    [Theory]
    [InlineData(29, 300, false)]
    [InlineData(30, 300, true)]
    [InlineData(300, 300, true)]
    [InlineData(9, 20, false)]
    [InlineData(10, 20, true)]
    [InlineData(24, 49, false)]
    [InlineData(25, 49, true)]
    [InlineData(0, 0.1, false)]
    [InlineData(-1, 20, false)]
    public void IsQualified_uses_listening_time_at_the_track_threshold(long seconds, double duration, bool expected)
    {
        ListeningPlayQualification.IsQualified(seconds, duration).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void IsQualified_unknown_or_invalid_duration_requires_thirty_seconds(double? duration)
    {
        ListeningPlayQualification.IsQualified(29, duration).Should().BeFalse();
        ListeningPlayQualification.IsQualified(30, duration).Should().BeTrue();
    }
}
