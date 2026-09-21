using Alexandria.Dto.Files.Streaming.Shuffle;
using Alexandria.Services.Streaming.Shuffle;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Streaming;

public class ShuffleRecencyTests
{
    private static readonly DateTime AsOf = new(2026, 9, 19, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(20.0, 10.0)]
    [InlineData(45.0, 22.5)]
    [InlineData(59.0, 29.5)]
    [InlineData(60.0, 30.0)]
    [InlineData(120.0, 30.0)]
    [InlineData(3600.0, 30.0)]
    public void ComputeThreshold_with_valid_duration_returns_half_capped_at_thirty(double duration, double expected)
    {
        ShuffleRanker.ComputeThreshold(duration).Should().BeApproximately(expected, 1e-9);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    public void ComputeThreshold_with_missing_or_non_positive_duration_returns_thirty(double? duration)
    {
        ShuffleRanker.ComputeThreshold(duration).Should().Be(30.0);
    }

    [Fact]
    public void ComputeThreshold_with_nan_duration_returns_thirty()
    {
        ShuffleRanker.ComputeThreshold(double.NaN).Should().Be(30.0);
    }

    [Fact]
    public void ComputeThreshold_with_infinite_duration_returns_thirty()
    {
        ShuffleRanker.ComputeThreshold(double.PositiveInfinity).Should().Be(30.0);
    }

    [Fact]
    public void ComputeWeight_with_never_heard_returns_four()
    {
        ShuffleRanker.ComputeWeight(null, AsOf).Should().Be(4.0);
    }

    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(1.0, 1.28)]
    [InlineData(7.0, 2.5)]
    [InlineData(14.0, 3.25)]
    [InlineData(28.0, 3.81)]
    public void ComputeWeight_matches_recency_curve(double daysAgo, double expected)
    {
        ShuffleRanker.ComputeWeight(AsOf.AddDays(-daysAgo), AsOf).Should().BeApproximately(expected, 0.01);
    }

    [Fact]
    public void ComputeWeight_is_monotonic_with_age()
    {
        var ages = new[] { 0.0, 1.0, 7.0, 14.0, 28.0, 365.0 };
        var weights = ages.Select(days => ShuffleRanker.ComputeWeight(AsOf.AddDays(-days), AsOf)).ToList();
        for (var i = 1; i < weights.Count; i++)
            weights[i].Should().BeGreaterThan(weights[i - 1]);
        weights[^1].Should().BeLessThan(4.0);
    }

    [Fact]
    public void ComputeWeight_with_future_listen_clamps_to_one()
    {
        ShuffleRanker.ComputeWeight(AsOf.AddHours(5), AsOf).Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void GetLastHeard_with_at_threshold_qualifies_and_just_below_does_not()
    {
        var atThreshold = new ShuffleListenRow
        {
            FileId = Guid.NewGuid(),
            ListenedSeconds = 30,
            EndedAtUtc = AsOf.AddDays(-2),
        };
        var justBelow = new ShuffleListenRow
        {
            FileId = atThreshold.FileId,
            ListenedSeconds = 29,
            EndedAtUtc = AsOf.AddDays(-1),
        };

        ShuffleRanker.GetLastHeard([justBelow], 60.0, AsOf).Should().BeNull();
        ShuffleRanker.GetLastHeard([atThreshold, justBelow], 60.0, AsOf).Should().Be(atThreshold.EndedAtUtc);
    }

    [Fact]
    public void GetLastHeard_with_fractional_threshold_requires_ceiled_integer_seconds()
    {
        var fileId = Guid.NewGuid();
        var below = new ShuffleListenRow { FileId = fileId, ListenedSeconds = 22, EndedAtUtc = AsOf.AddDays(-3) };
        var atCeil = new ShuffleListenRow { FileId = fileId, ListenedSeconds = 23, EndedAtUtc = AsOf.AddDays(-1) };

        ShuffleRanker.GetLastHeard([below], 45.0, AsOf).Should().BeNull();
        ShuffleRanker.GetLastHeard([below, atCeil], 45.0, AsOf).Should().Be(atCeil.EndedAtUtc);
    }

    [Fact]
    public void GetLastHeard_returns_maximum_qualifying_ended_at()
    {
        var fileId = Guid.NewGuid();
        var older = new ShuffleListenRow { FileId = fileId, ListenedSeconds = 60, EndedAtUtc = AsOf.AddDays(-10) };
        var newer = new ShuffleListenRow { FileId = fileId, ListenedSeconds = 60, EndedAtUtc = AsOf.AddDays(-1) };

        ShuffleRanker.GetLastHeard([newer, older], 120.0, AsOf).Should().Be(newer.EndedAtUtc);
    }

    [Fact]
    public void GetLastHeard_with_no_rows_returns_null()
    {
        ShuffleRanker.GetLastHeard([], 120.0, AsOf).Should().BeNull();
    }
}