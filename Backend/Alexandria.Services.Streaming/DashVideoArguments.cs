using System.Globalization;
using Alexandria.Common.Processes;

namespace Alexandria.Services.Streaming;

/// <summary>
/// Builds the ffmpeg command for one video DASH pass: one encoded video stream per lane plus,
/// when the source has one, a single audio stream.
/// </summary>
internal static class DashVideoArguments
{
    public const int SegmentSeconds = 6;

    // The VBV buffer spans two seconds at the lane's ceiling, so short peaks average out
    // without letting a lane drift above the bandwidth its manifest entry promises.
    private const int BufferSeconds = 2;

    public static string ForCpu(
        string inputPath,
        string manifestPath,
        IReadOnlyList<VideoQualityLane> lanes,
        bool hasAudio)
    {
        var args = new List<string> { FfmpegThreads.Input, "-i", Quote(inputPath) };

        // Every option carries the lane's stream specifier: without one ffmpeg applies the last
        // value given to all lanes. Only the first real video stream is used: `V` skips attached
        // pictures such as embedded cover art, which would otherwise become extra lanes with their
        // own aspect ratio and make the DASH muxer refuse the whole pass.
        for (var i = 0; i < lanes.Count; i++)
        {
            var lane = lanes[i];

            args.AddRange([
                "-map", "0:V:0",
                $"-filter:v:{i}", $"scale={lane.Width}:{lane.Height}",
                $"-c:v:{i}", "libx264",
                $"-preset:v:{i}", "fast",
                $"-crf:v:{i}", Number(lane.Crf),

                // With CRF set libx264 only reports this rate; the DASH muxer writes it as the
                // lane's bandwidth, which players use to choose a lane.
                $"-b:v:{i}", Kbps(lane.BitrateKbps),
                $"-maxrate:v:{i}", Kbps(lane.BitrateKbps),
                $"-bufsize:v:{i}", Kbps(lane.BitrateKbps * BufferSeconds),
            ]);
        }

        // Segments can only start on a keyframe. Forcing one at every segment boundary keeps
        // segments at their intended length and at the same instants in every lane, so a player
        // can switch lanes between any two segments.
        args.AddRange(["-force_key_frames", $"\"expr:gte(t,n_forced*{Number(SegmentSeconds)})\""]);

        // The manifest's audio set would be empty without an audio stream, which players reject.
        var adaptationSets = "id=0,streams=v";

        if (hasAudio)
        {
            args.AddRange(["-map", "0:a:0", "-c:a", "aac", "-b:a", "128k"]);
            adaptationSets += " id=1,streams=a";
        }

        args.AddRange([
            "-f", "dash",
            "-seg_duration", Number(SegmentSeconds),
            "-adaptation_sets", $"\"{adaptationSets}\"",
            FfmpegThreads.Output,
            Quote(manifestPath),
        ]);

        return string.Join(" ", args);
    }

    /// <summary>
    /// ffprobe arguments that print the index of the source's first audio stream, and nothing
    /// when it has none.
    /// </summary>
    public static string AudioProbe(string inputPath) =>
        $"-v error -select_streams a:0 -show_entries stream=index -of csv=p=0 {Quote(inputPath)}";

    private static string Quote(string path) => $"\"{path}\"";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Kbps(int value) => $"{Number(value)}k";
}
