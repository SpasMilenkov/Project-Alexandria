using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Streaming.VideoEncoding;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Streaming;

public class VideoLadderTests
{
    private static (int Width, int Height)[] Sizes(int width, int height, params VideoRung[] rungs) =>
        VideoLadder.Fit(new VideoSource(width, height, true), rungs)
            .Select(lane => (lane.Width, lane.Height))
            .ToArray();

    [Fact]
    public void Fit_landscape1080_keepsTheRungSizes()
    {
        Sizes(1920, 1080, VideoRung.P1080, VideoRung.P360).Should().Equal((640, 360), (1920, 1080));
    }

    [Fact]
    public void Fit_portrait1080_turnsEveryLaneUpright()
    {
        Sizes(1080, 1920, VideoRung.P360, VideoRung.P1080).Should().Equal((360, 640), (1080, 1920));
    }

    [Fact]
    public void Fit_widescreenFilm_keepsItsShapeInsideEachBox()
    {
        Sizes(1920, 800, VideoRung.P720, VideoRung.P1080).Should().Equal((1280, 534), (1920, 800));
    }

    [Fact]
    public void Fit_rungsAboveTheSource_giveWayToOneLaneAtTheSourceSize()
    {
        var lanes = VideoLadder.Fit(new VideoSource(1280, 720, true),
            [VideoRung.P360, VideoRung.P1080, VideoRung.P2160]);

        lanes.Should().Equal(new VideoQualityLane(640, 360, 800, 28), new VideoQualityLane(1280, 720, 2500, 23));
    }

    [Fact]
    public void Fit_sourceBetweenRungs_takesTheSettingsOfTheRungItFitsIn()
    {
        VideoLadder.Fit(new VideoSource(1000, 562, true), [VideoRung.P1080])
            .Should().Equal(new VideoQualityLane(1000, 562, 2500, 23));
    }

    [Fact]
    public void Fit_sourceBelowEveryRung_stillGetsOneLane()
    {
        VideoLadder.Fit(new VideoSource(160, 90, true), [VideoRung.P360, VideoRung.P720])
            .Should().Equal(new VideoQualityLane(160, 90, 800, 28));
    }

    [Fact]
    public void Fit_rungMatchingTheSource_isNotRepeatedBySourceSizeLane()
    {
        Sizes(1280, 720, VideoRung.P720, VideoRung.P1080).Should().Equal((1280, 720));
    }

    [Fact]
    public void Fit_oddSource_roundsEveryLaneToEvenSides()
    {
        Sizes(1365, 767, VideoRung.P360, VideoRung.P1080).Should().Equal((640, 360), (1366, 768));
    }

    [Fact]
    public void Fit_repeatedRung_encodesItOnce()
    {
        Sizes(1920, 1080, VideoRung.P720, VideoRung.P720).Should().Equal((1280, 720));
    }
}
