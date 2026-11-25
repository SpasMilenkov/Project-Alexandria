using Alexandria.Services.Streaming;
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

            ValueAfter(tokens, $"-filter:v:{i}").Should().Be($"scale={lane.Width}:{lane.Height}");
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

    [Fact]
    public void AudioProbe_selectsOnlyTheFirstAudioStreamOfTheQuotedSource()
    {
        DashVideoArguments.AudioProbe("/in/my source").Should()
            .Be("-v error -select_streams a:0 -show_entries stream=index -of csv=p=0 \"/in/my source\"");
    }
}
