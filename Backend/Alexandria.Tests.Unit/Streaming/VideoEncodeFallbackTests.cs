using Alexandria.Common.Exceptions.Transpilation;
using Alexandria.Services.Streaming.VideoEncoding;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Streaming;

public class VideoEncodeFallbackTests
{
    private readonly List<VideoEncodeAttempt> _ran = [];
    private readonly List<(VideoEncodeAttempt Failed, VideoEncodeAttempt Next)> _fallbacks = [];

    private static readonly IReadOnlyList<VideoEncodeAttempt> GpuAttempts =
        VideoEncodeFallback.AttemptsFor(new VideoEncoderSelection("/dev/dri/renderD128"));

    private Task RunAsync(Func<VideoEncodeAttempt, Exception?> outcome) =>
        VideoEncodeFallback.RunAsync(
            GpuAttempts,
            attempt =>
            {
                _ran.Add(attempt);
                return outcome(attempt) is { } failure ? Task.FromException(failure) : Task.CompletedTask;
            },
            (failed, next, _) => _fallbacks.Add((failed, next)));

    private static TranspilationFfmpegException FfmpegFailure() => new("/in/source", "video DASH", 1, "boom");

    [Fact]
    public void AttemptsFor_gpuOrCpu_ordersTheGpuAttemptsBeforeTheCpu()
    {
        GpuAttempts.Should().Equal(
            VideoEncodeAttempt.GpuDecodeAndEncode, VideoEncodeAttempt.CpuDecodeGpuEncode, VideoEncodeAttempt.Cpu);
        VideoEncodeFallback.AttemptsFor(VideoEncoderSelection.Cpu).Should().Equal(VideoEncodeAttempt.Cpu);
    }

    [Fact]
    public async Task RunAsync_firstAttemptSucceeds_runsNothingElse()
    {
        await RunAsync(_ => null);

        _ran.Should().Equal(VideoEncodeAttempt.GpuDecodeAndEncode);
        _fallbacks.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_gpuDecodeFails_retriesWithCpuDecodeOnTheGpu()
    {
        await RunAsync(attempt => attempt == VideoEncodeAttempt.GpuDecodeAndEncode ? FfmpegFailure() : null);

        _ran.Should().Equal(VideoEncodeAttempt.GpuDecodeAndEncode, VideoEncodeAttempt.CpuDecodeGpuEncode);
        _fallbacks.Should().Equal((VideoEncodeAttempt.GpuDecodeAndEncode, VideoEncodeAttempt.CpuDecodeGpuEncode));
    }

    [Fact]
    public async Task RunAsync_bothGpuAttemptsFail_finishesOnTheCpu()
    {
        await RunAsync(attempt => attempt == VideoEncodeAttempt.Cpu ? null : FfmpegFailure());

        _ran.Should().Equal(GpuAttempts);
        _fallbacks.Should().HaveCount(2);
    }

    [Fact]
    public async Task RunAsync_everyAttemptFails_reportsTheCpuFailure()
    {
        var act = () => RunAsync(_ => FfmpegFailure());

        await act.Should().ThrowAsync<TranspilationFfmpegException>();
        _ran.Should().Equal(GpuAttempts);
    }

    [Fact]
    public async Task RunAsync_cancelledJob_stopsWithoutTryingTheNextAttempt()
    {
        var act = () => RunAsync(_ => new TranspilationCancelledException(Guid.NewGuid()));

        await act.Should().ThrowAsync<TranspilationCancelledException>();
        _ran.Should().Equal(VideoEncodeAttempt.GpuDecodeAndEncode);
    }

    [Fact]
    public async Task RunAsync_shutdown_stopsWithoutTryingTheNextAttempt()
    {
        var act = () => RunAsync(_ => new OperationCanceledException());

        await act.Should().ThrowAsync<OperationCanceledException>();
        _ran.Should().Equal(VideoEncodeAttempt.GpuDecodeAndEncode);
    }
}
