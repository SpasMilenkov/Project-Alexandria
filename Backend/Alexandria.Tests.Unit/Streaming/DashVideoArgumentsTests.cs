using Alexandria.Services.Streaming.VideoEncoding;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Streaming;

public class DashVideoArgumentsTests
{
    private static readonly VideoQualityLane[] Ladder =
    [
        new(640, 360, 800, 28),
        new(1280, 720, 2500, 23),
        new(1920, 1080, 5000, 21),
    ];

    private static string Build(bool hasAudio = true) =>
        DashVideoArguments.ForCpu("/in/source", "/out/manifest.mpd", Ladder, hasAudio);

    private static string[] Tokens(string arguments) => arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static string ValueAfter(string[] tokens, string option)
    {
        var index = Array.IndexOf(tokens, option);
        index.Should().BeGreaterThanOrEqualTo(0, $"{option} should be present");

        return tokens[index + 1];
    }

    [Fact]
    public void ForCpu_ladder_givesEveryLaneItsOwnQualityCapAndSize()
    {
        var tokens = Tokens(Build());

        for (var i = 0; i < Ladder.Length; i++)
        {
            var lane = Ladder[i];

            ValueAfter(tokens, $"-filter:v:{i}").Should().Be($"scale={lane.Width}:{lane.Height},setsar=1");
            ValueAfter(tokens, $"-c:v:{i}").Should().Be("libx264");
            ValueAfter(tokens, $"-preset:v:{i}").Should().Be("fast");
            ValueAfter(tokens, $"-crf:v:{i}").Should().Be(lane.Crf.ToString());
            ValueAfter(tokens, $"-b:v:{i}").Should().Be($"{lane.BitrateKbps}k");
            ValueAfter(tokens, $"-maxrate:v:{i}").Should().Be($"{lane.BitrateKbps}k");
            ValueAfter(tokens, $"-bufsize:v:{i}").Should().Be($"{lane.BitrateKbps * 2}k");
        }
    }

    // An option without a stream specifier applies to every lane, and the last one given wins.
    [Fact]
    public void ForCpu_ladder_neverSetsALaneOptionForAllStreams()
    {
        var tokens = Tokens(Build());

        tokens.Should().NotContain(["-crf", "-preset", "-vf", "-filter:v", "-b:v", "-maxrate", "-bufsize"]);
        tokens.Should().NotContain(token => token.StartsWith("-vf:", StringComparison.Ordinal),
            "ffmpeg reads -vf:N as -filter:v for every video stream");
    }

    [Fact]
    public void ForCpu_sourceWithAudio_mapsOnlyTheFirstVideoAndAudioStreams()
    {
        var tokens = Tokens(Build());
        var maps = tokens.Where((_, index) => index > 0 && tokens[index - 1] == "-map").ToList();

        maps.Should().Equal([.. Enumerable.Repeat("0:V:0", Ladder.Length), "0:a:0"]);
        ValueAfter(tokens, "-c:a").Should().Be("aac");
        Build().Should().Contain("-adaptation_sets \"id=0,streams=v id=1,streams=a\"");
    }

    [Fact]
    public void ForCpu_sourceWithoutAudio_leavesAudioOutOfTheCommandAndTheManifest()
    {
        var arguments = Build(hasAudio: false);

        arguments.Should().NotContain("0:a").And.NotContain("-c:a").And.NotContain("streams=a");
        arguments.Should().Contain("-adaptation_sets \"id=0,streams=v\"");
    }

    [Fact]
    public void ForCpu_ladder_forcesAKeyframeAtEverySegmentBoundary()
    {
        var tokens = Tokens(Build());

        ValueAfter(tokens, "-force_key_frames").Should().Be("\"expr:gte(t,n_forced*6)\"");
        ValueAfter(tokens, "-seg_duration").Should().Be("6");
    }

    [Fact]
    public void ForCpu_paths_areQuotedWithTheInputFirstAndTheManifestLast()
    {
        var arguments = DashVideoArguments.ForCpu("/in/my source", "/out/dash dir/manifest.mpd", Ladder, hasAudio: true);

        arguments.Should().Contain("-i \"/in/my source\"");
        arguments.Should().EndWith("\"/out/dash dir/manifest.mpd\"");
    }

    private static string BuildGpu(
        GpuRateControl rateControl = GpuRateControl.ConstantQuality,
        bool gpuDecode = true,
        bool hasAudio = true) =>
        DashVideoArguments.ForGpu("/in/source", "/out/manifest.mpd", Ladder, hasAudio, "/dev/dri/renderD128",
            gpuDecode, rateControl);

    [Fact]
    public void ForGpu_ladder_scalesEveryLaneOnTheGpuAndMapsItsOutput()
    {
        var arguments = BuildGpu();
        var tokens = Tokens(arguments);

        arguments.Should().Contain("-init_hw_device \"vaapi=va:/dev/dri/renderD128\" -filter_hw_device va");
        arguments.Should().Contain(
            "\"[0:V:0]format=nv12,hwupload,split=3[s0][s1][s2];" +
            "[s0]scale_vaapi=w=640:h=360:extra_hw_frames=16,setsar=1[v0];" +
            "[s1]scale_vaapi=w=1280:h=720:extra_hw_frames=16,setsar=1[v1];" +
            "[s2]scale_vaapi=w=1920:h=1080:extra_hw_frames=16,setsar=1[v2]\"");

        tokens.Where((_, index) => index > 0 && tokens[index - 1] == "-map").Should()
            .Equal("[v0]", "[v1]", "[v2]", "0:a:0");
    }

    [Fact]
    public void ForGpu_constantQuality_usesTheLaneQualityAsQpAndAdvertisesTheCeiling()
    {
        var tokens = Tokens(BuildGpu(GpuRateControl.ConstantQuality));

        for (var i = 0; i < Ladder.Length; i++)
        {
            var lane = Ladder[i];

            ValueAfter(tokens, $"-c:v:{i}").Should().Be("h264_vaapi");
            ValueAfter(tokens, $"-rc_mode:v:{i}").Should().Be("CQP");
            ValueAfter(tokens, $"-qp:v:{i}").Should().Be(lane.Crf.ToString());
            ValueAfter(tokens, $"-b:v:{i}").Should().Be($"{lane.BitrateKbps}k");
        }

        tokens.Should().NotContain(token => token.StartsWith("-maxrate", StringComparison.Ordinal));
    }

    [Fact]
    public void ForGpu_cappedBitrate_holdsEveryLaneToItsCeiling()
    {
        var tokens = Tokens(BuildGpu(GpuRateControl.CappedBitrate));

        for (var i = 0; i < Ladder.Length; i++)
        {
            var lane = Ladder[i];

            ValueAfter(tokens, $"-rc_mode:v:{i}").Should().Be("VBR");
            ValueAfter(tokens, $"-b:v:{i}").Should().Be($"{lane.BitrateKbps}k");
            ValueAfter(tokens, $"-maxrate:v:{i}").Should().Be($"{lane.BitrateKbps}k");
            ValueAfter(tokens, $"-bufsize:v:{i}").Should().Be($"{lane.BitrateKbps * 2}k");
        }

        tokens.Should().NotContain(token => token.StartsWith("-qp", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ForGpu_gpuDecode_addsTheHardwareDecoderOnlyWhenAsked(bool gpuDecode)
    {
        BuildGpu(gpuDecode: gpuDecode).Contains("-hwaccel vaapi -hwaccel_device va -i").Should().Be(gpuDecode);
    }

    [Fact]
    public void ForGpu_sharesSegmentsAndAudioRulesWithTheCpuCommand()
    {
        var gpu = BuildGpu(hasAudio: false);
        var cpu = Build(hasAudio: false);

        gpu[gpu.IndexOf("-force_key_frames", StringComparison.Ordinal)..].Should()
            .Be(cpu[cpu.IndexOf("-force_key_frames", StringComparison.Ordinal)..]);
    }
}
