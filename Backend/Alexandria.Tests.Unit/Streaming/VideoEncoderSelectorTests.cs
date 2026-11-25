using Alexandria.Common.Config;
using Alexandria.Services.Streaming.VideoEncoding;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Alexandria.Tests.Unit.Streaming;

public sealed class VideoEncoderSelectorTests : IDisposable
{
    // Any file that exists stands in for a render node; the probe itself is substituted.
    private readonly string _presentDevice = Path.GetTempFileName();

    private readonly IGpuEncoderProbe _probe = Substitute.For<IGpuEncoderProbe>();

    public void Dispose() => File.Delete(_presentDevice);

    private VideoEncoderSelector CreateSut(VideoEncoderMode mode = VideoEncoderMode.Auto, string? device = null) =>
        new(Options.Create(new VideoEncodingConfig { Mode = mode, Device = device ?? _presentDevice }),
            _probe, NullLogger<VideoEncoderSelector>.Instance);

    private void ProbeReturns(bool usable) =>
        _probe.ProbeAsync(_presentDevice, Arg.Any<CancellationToken>())
            .Returns(new GpuProbeResult(_presentDevice, usable, "test driver", usable ? null : "no encoder"));

    [Fact]
    public async Task SelectAsync_usableGpu_encodesOnIt()
    {
        ProbeReturns(usable: true);
        using var sut = CreateSut();

        var selection = await sut.SelectAsync(TestContext.Current.CancellationToken);

        selection.Should().Be(new VideoEncoderSelection(_presentDevice));
        selection.UsesGpu.Should().BeTrue();
    }

    [Fact]
    public async Task SelectAsync_gpuFailingItsProbe_usesTheCpu()
    {
        ProbeReturns(usable: false);
        using var sut = CreateSut();

        var selection = await sut.SelectAsync(TestContext.Current.CancellationToken);

        selection.Should().Be(VideoEncoderSelection.Cpu);
    }

    [Fact]
    public async Task SelectAsync_cpuMode_neverProbes()
    {
        using var sut = CreateSut(VideoEncoderMode.Cpu);

        var selection = await sut.SelectAsync(TestContext.Current.CancellationToken);

        selection.Should().Be(VideoEncoderSelection.Cpu);
        await _probe.DidNotReceiveWithAnyArgs().ProbeAsync(default!, default);
    }

    [Fact]
    public async Task SelectAsync_noDeviceInTheContainer_usesTheCpuWithoutProbing()
    {
        using var sut = CreateSut(device: "/dev/dri/no-such-node");

        var selection = await sut.SelectAsync(TestContext.Current.CancellationToken);

        selection.Should().Be(VideoEncoderSelection.Cpu);
        await _probe.DidNotReceiveWithAnyArgs().ProbeAsync(default!, default);
    }

    [Fact]
    public async Task SelectAsync_probeThrowing_usesTheCpuInsteadOfFailing()
    {
        _probe.ProbeAsync(_presentDevice, Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("broken pipe"));
        using var sut = CreateSut();

        var selection = await sut.SelectAsync(TestContext.Current.CancellationToken);

        selection.Should().Be(VideoEncoderSelection.Cpu);
    }

    [Fact]
    public async Task SelectAsync_repeatedAndConcurrentCalls_probeOnce()
    {
        ProbeReturns(usable: true);
        using var sut = CreateSut();
        var ct = TestContext.Current.CancellationToken;

        var selections = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => sut.SelectAsync(ct)));
        await sut.SelectAsync(ct);

        selections.Should().OnlyContain(selection => selection.UsesGpu);
        await _probe.Received(1).ProbeAsync(_presentDevice, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectAsync_cancelledProbe_isAskedAgainNextTime()
    {
        _probe.ProbeAsync(_presentDevice, Arg.Any<CancellationToken>())
            .Returns(
                _ => throw new OperationCanceledException(),
                _ => new GpuProbeResult(_presentDevice, true, "test driver", null));
        using var sut = CreateSut();
        var ct = TestContext.Current.CancellationToken;

        await sut.Invoking(selector => selector.SelectAsync(ct)).Should().ThrowAsync<OperationCanceledException>();
        var selection = await sut.SelectAsync(ct);

        selection.UsesGpu.Should().BeTrue();
    }
}
