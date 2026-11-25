using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Services.Streaming.VideoEncoding;

/// <summary>
/// Turns the requested rungs into lanes cut to the source's shape. A rung names the box a lane
/// fits in, landscape here and turned for a portrait source, so a 1080p lane of a phone clip is
/// 1080 wide and a 2.39:1 film stays 2.39:1.
/// </summary>
internal static class VideoLadder
{
    private static readonly IReadOnlyDictionary<VideoRung, VideoQualityLane> Rungs =
        new Dictionary<VideoRung, VideoQualityLane>
        {
            [VideoRung.P360] = new(640, 360, 800, 28),
            [VideoRung.P480] = new(854, 480, 1500, 26),
            [VideoRung.P720] = new(1280, 720, 2500, 23),
            [VideoRung.P1080] = new(1920, 1080, 5000, 21),
            [VideoRung.P1440] = new(2560, 1440, 8000, 19),
            [VideoRung.P2160] = new(3840, 2160, 16000, 17),
        };

    public static IReadOnlyList<VideoQualityLane> Fit(VideoSource source, IEnumerable<VideoRung> rungs)
    {
        var requested = rungs.Distinct().Select(rung => Rungs[rung]).ToList();

        var lanes = requested
            .Where(rung => !Upscales(rung, source))
            .Select(rung => Scaled(rung, source))
            .ToList();

        // A rung above the source would spend its bits on detail the source never had. The
        // source's own size takes its place, so the sharpest lane is still as sharp as the source.
        if (requested.Any(rung => Upscales(rung, source)))
            lanes.Add(AtSourceSize(source));

        return lanes
            .DistinctBy(lane => (lane.Width, lane.Height))
            .OrderBy(lane => lane.Width * lane.Height)
            .ToList();
    }

    // Smaller than the box on both sides: every pixel of the lane would be made up.
    private static bool Upscales(VideoQualityLane rung, VideoSource source) =>
        source.LongSide < LongSide(rung) && source.ShortSide < ShortSide(rung);

    private static VideoQualityLane Scaled(VideoQualityLane rung, VideoSource source)
    {
        var factor = Math.Min(1.0, Math.Min(
            (double)LongSide(rung) / source.LongSide,
            (double)ShortSide(rung) / source.ShortSide));

        return rung with { Width = Even(source.Width * factor), Height = Even(source.Height * factor) };
    }

    // Takes the ceiling and quality of the smallest rung the source fits in, the rung it would
    // have been encoded at had it been asked for.
    private static VideoQualityLane AtSourceSize(VideoSource source)
    {
        var settings = Rungs.Values
            .OrderBy(rung => rung.Width * rung.Height)
            .FirstOrDefault(rung => source.LongSide <= LongSide(rung) && source.ShortSide <= ShortSide(rung))
            ?? Rungs[VideoRung.P2160];

        return settings with { Width = Even(source.Width), Height = Even(source.Height) };
    }

    private static int LongSide(VideoQualityLane lane) => Math.Max(lane.Width, lane.Height);

    private static int ShortSide(VideoQualityLane lane) => Math.Min(lane.Width, lane.Height);

    // H.264 in 4:2:0 needs even sides.
    private static int Even(double value) => Math.Max(2, (int)Math.Round(value / 2, MidpointRounding.AwayFromZero) * 2);
}
