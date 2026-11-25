using Alexandria.Services.Streaming.VideoEncoding;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Streaming;

public class VideoSourceProbeTests
{
    private const string Audio = """{ "codec_type": "audio", "disposition": { "attached_pic": 0 } }""";

    private static string Video(int width, int height, string sar = "1:1", int attachedPic = 0, string sideData = "") =>
        $$"""
        {
            "codec_type": "video", "width": {{width}}, "height": {{height}},
            "sample_aspect_ratio": "{{sar}}", "disposition": { "attached_pic": {{attachedPic}} }{{sideData}}
        }
        """;

    private static string Rotation(int degrees) => $$""", "side_data_list": [ { "rotation": {{degrees}} } ]""";

    private static string Probe(params string[] streams) =>
        $$"""{ "programs": [], "streams": [ {{string.Join(", ", streams)}} ] }""";

    [Fact]
    public void Arguments_quoteTheSourceAndAskForJson()
    {
        VideoSourceProbe.Arguments("/in/my source").Should().Be(
            "-v error -show_entries stream=codec_type,width,height,sample_aspect_ratio" +
            ":stream_disposition=attached_pic:stream_side_data=rotation -of json \"/in/my source\"");
    }

    [Fact]
    public void Parse_landscapeWithAudio_keepsItsSize()
    {
        VideoSourceProbe.Parse(Probe(Video(1920, 1080), Audio)).Should().Be(new VideoSource(1920, 1080, true));
    }

    [Fact]
    public void Parse_noAudioStream_reportsNoAudio()
    {
        VideoSourceProbe.Parse(Probe(Video(1280, 720)))!.HasAudio.Should().BeFalse();
    }

    [Theory]
    [InlineData(90)]
    [InlineData(-90)]
    [InlineData(270)]
    public void Parse_quarterTurnTag_swapsTheSides(int degrees)
    {
        VideoSourceProbe.Parse(Probe(Video(1920, 1080, sideData: Rotation(degrees))))
            .Should().Be(new VideoSource(1080, 1920, false));
    }

    [Fact]
    public void Parse_halfTurnTag_keepsTheSides()
    {
        VideoSourceProbe.Parse(Probe(Video(1920, 1080, sideData: Rotation(180))))
            .Should().Be(new VideoSource(1920, 1080, false));
    }

    [Fact]
    public void Parse_anamorphicSource_measuresTheShownWidth()
    {
        VideoSourceProbe.Parse(Probe(Video(1440, 1080, sar: "4:3"))).Should().Be(new VideoSource(1920, 1080, false));
    }

    [Theory]
    [InlineData("0:1")]
    [InlineData("N/A")]
    public void Parse_unknownPixelShape_assumesSquarePixels(string sar)
    {
        VideoSourceProbe.Parse(Probe(Video(1280, 720, sar: sar))).Should().Be(new VideoSource(1280, 720, false));
    }

    [Fact]
    public void Parse_coverArtBeforeTheVideo_measuresTheVideo()
    {
        VideoSourceProbe.Parse(Probe(Video(600, 600, attachedPic: 1), Video(1280, 720), Audio))
            .Should().Be(new VideoSource(1280, 720, true));
    }

    [Fact]
    public void Parse_onlyCoverArt_findsNoVideo()
    {
        VideoSourceProbe.Parse(Probe(Video(600, 600, attachedPic: 1), Audio)).Should().BeNull();
    }

    [Fact]
    public void Parse_noStreams_findsNoVideo()
    {
        VideoSourceProbe.Parse("{}").Should().BeNull();
    }
}
