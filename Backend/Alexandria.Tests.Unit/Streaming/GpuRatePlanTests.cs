using Alexandria.Common.Exceptions.Transpilation;
using Alexandria.Services.Streaming.VideoEncoding;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Streaming;

public class GpuRatePlanTests
{
    private readonly GpuRatePlan _plan = new();
    private readonly List<GpuRateControl> _passes = [];
    private readonly List<string> _overshoots = [];

    private Task RunAsync(string? problem = null, GpuRateControl? failingPass = null) =>
        _plan.RunAsync(
            rateControl =>
            {
                _passes.Add(rateControl);

                if (rateControl == failingPass)
                    throw new TranspilationFfmpegException("source", "video DASH", 1, "driver error");

                return Task.CompletedTask;
            },
            () => problem,
            _overshoots.Add);

    [Fact]
    public async Task RunAsync_withinCeilings_runsOnlyConstantQuality()
    {
        await RunAsync();

        _passes.Should().Equal(GpuRateControl.ConstantQuality);
        _overshoots.Should().BeEmpty();
        _plan.KnownToOvershoot.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_overshoot_reportsItThenEncodesCapped()
    {
        await RunAsync(problem: "lane 0 peaked");

        _passes.Should().Equal(GpuRateControl.ConstantQuality, GpuRateControl.CappedBitrate);
        _overshoots.Should().Equal("lane 0 peaked");
    }

    // The case that wasted a pass: the capped re-encode fails, and the next attempt would have
    // repeated a constant-quality pass already known to overshoot.
    [Fact]
    public async Task RunAsync_cappedPassFailedEarlier_nextAttemptGoesStraightToCapped()
    {
        var first = () => RunAsync(problem: "lane 0 peaked", failingPass: GpuRateControl.CappedBitrate);
        await first.Should().ThrowAsync<TranspilationFfmpegException>();
        _passes.Clear();

        await RunAsync(problem: "lane 0 peaked");

        _passes.Should().Equal(GpuRateControl.CappedBitrate);
        _overshoots.Should().ContainSingle("the overshoot was already reported");
        _plan.KnownToOvershoot.Should().BeTrue();
    }

    // A failed constant-quality pass says nothing about overshooting, so the next attempt tries
    // constant quality again.
    [Fact]
    public async Task RunAsync_constantQualityFailedEarlier_nextAttemptStartsWithConstantQuality()
    {
        var first = () => RunAsync(failingPass: GpuRateControl.ConstantQuality);
        await first.Should().ThrowAsync<TranspilationFfmpegException>();
        _passes.Clear();

        await RunAsync();

        _passes.Should().Equal(GpuRateControl.ConstantQuality);
        _plan.KnownToOvershoot.Should().BeFalse();
    }
}
