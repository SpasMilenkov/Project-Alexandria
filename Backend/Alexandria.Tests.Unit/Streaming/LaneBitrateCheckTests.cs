using Alexandria.Services.Streaming.VideoEncoding;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Streaming;

public sealed class LaneBitrateCheckTests : IDisposable
{
    // Three 6 s segments and a 2 s tail per lane, as ffmpeg writes them for a 20 s source.
    private const string Manifest = """
        <?xml version="1.0" encoding="utf-8"?>
        <MPD xmlns="urn:mpeg:dash:schema:mpd:2011" type="static">
          <Period id="0" start="PT0.0S">
            <AdaptationSet id="0" contentType="video">
              <Representation id="0" bandwidth="800000" width="640" height="360">
                <SegmentTemplate timescale="15360" initialization="init-stream$RepresentationID$.m4s" media="chunk-stream$RepresentationID$-$Number%05d$.m4s" startNumber="1">
                  <SegmentTimeline>
                    <S t="0" d="92160" r="2" />
                    <S d="30720" />
                  </SegmentTimeline>
                </SegmentTemplate>
              </Representation>
              <Representation id="1" bandwidth="2500000" width="1280" height="720">
                <SegmentTemplate timescale="15360" initialization="init-stream$RepresentationID$.m4s" media="chunk-stream$RepresentationID$-$Number%05d$.m4s" startNumber="1">
                  <SegmentTimeline>
                    <S t="0" d="92160" r="2" />
                    <S d="30720" />
                  </SegmentTimeline>
                </SegmentTemplate>
              </Representation>
            </AdaptationSet>
          </Period>
        </MPD>
        """;

    private static readonly VideoQualityLane[] Lanes =
    [
        new(640, 360, 800, 28),
        new(1280, 720, 2500, 23),
    ];

    private readonly string _directory = Directory.CreateTempSubdirectory("lane-bitrate-check-").FullName;

    private string ManifestPath => Path.Combine(_directory, "manifest.mpd");

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    // Sizes are given as a share of the lane's ceiling over each segment's own length.
    private void WriteLanes(double sixSecondShare = 0.5, double tailShare = 0.5, int? missingSegment = null)
    {
        File.WriteAllText(ManifestPath, Manifest);

        for (var lane = 0; lane < Lanes.Length; lane++)
        {
            for (var number = 1; number <= 4; number++)
            {
                if (lane == 1 && number == missingSegment)
                    continue;

                var seconds = number == 4 ? 2 : 6;
                var share = number == 4 ? tailShare : sixSecondShare;
                var bytes = (long)(Lanes[lane].BitrateKbps * 1000 / 8.0 * seconds * share);

                using var segment = File.Create(Path.Combine(_directory, $"chunk-stream{lane}-{number:D5}.m4s"));
                segment.SetLength(bytes);
            }
        }
    }

    [Fact]
    public void FindProblem_lanesWellUnderTheirCeilings_fits()
    {
        WriteLanes();

        LaneBitrateCheck.FindProblem(ManifestPath, Lanes).Should().BeNull();
    }

    // A 2 s buffer lets a 6 s segment carry (6 + 2) / 6 of the ceiling.
    [Theory]
    [InlineData(1.30, true)]
    [InlineData(1.40, false)]
    public void FindProblem_sixSecondSegment_fitsUpToTheBufferAllowance(double share, bool fits)
    {
        WriteLanes(sixSecondShare: share);

        var problem = LaneBitrateCheck.FindProblem(ManifestPath, Lanes);

        (problem is null).Should().Be(fits, problem);
    }

    // The same buffer is a larger share of a short segment: (2 + 2) / 2 of the ceiling.
    [Theory]
    [InlineData(1.90, true)]
    [InlineData(2.10, false)]
    public void FindProblem_shortLastSegment_isMeasuredOverItsOwnLength(double share, bool fits)
    {
        WriteLanes(tailShare: share);

        (LaneBitrateCheck.FindProblem(ManifestPath, Lanes) is null).Should().Be(fits);
    }

    [Fact]
    public void FindProblem_overshoot_namesTheLaneTheSegmentAndTheRates()
    {
        WriteLanes(sixSecondShare: 1.5);

        LaneBitrateCheck.FindProblem(ManifestPath, Lanes).Should()
            .Be("lane 0 (640x360) peaked at 1200 kbps in chunk-stream0-00001.m4s, above the 1067 kbps its 800 kbps ceiling allows");
    }

    [Fact]
    public void FindProblem_missingSegment_doesNotFit()
    {
        WriteLanes(missingSegment: 2);

        LaneBitrateCheck.FindProblem(ManifestPath, Lanes).Should().Be("segment chunk-stream1-00002.m4s of lane 1 is missing");
    }

    [Fact]
    public void FindProblem_laneMissingFromTheManifest_doesNotFit()
    {
        WriteLanes();

        LaneBitrateCheck.FindProblem(ManifestPath, [.. Lanes, new VideoQualityLane(1920, 1080, 5000, 21)]).Should()
            .Be("lane 2 is missing from the manifest");
    }

    [Fact]
    public void FindProblem_unreadableManifest_doesNotFit()
    {
        File.WriteAllText(ManifestPath, "<MPD");

        LaneBitrateCheck.FindProblem(ManifestPath, Lanes).Should().StartWith("the manifest could not be read");
    }
}
