using System.Globalization;
using Alexandria.Common.Processes;

namespace Alexandria.Services.Streaming.VideoEncoding;

/// <summary>
/// Builds the ffmpeg command for one video DASH pass: one encoded video stream per lane plus,
/// when the source has one, a single audio stream. The CPU and GPU commands produce the same
/// lanes, segments and manifest layout.
/// </summary>
internal static class DashVideoArguments
{
    public const int SegmentSeconds = 6;

    // The VBV buffer spans two seconds at the lane's ceiling, so short peaks average out
    // without letting a lane drift above the bandwidth its manifest entry promises.
    public const int BufferSeconds = 2;

    // Each encoder keeps frames queued while the scaler feeding it produces more. The default
    // pool of a VAAPI scaler runs dry at the end of a multi-lane pass with "Cannot allocate
    // memory". 8 extra frames still failed on a three-lane ladder, 16 passed with all six lanes.
    private const int ExtraGpuFrames = 16;

    // Lanes are sized from the source as shown, so their pixels are square. Saying so outright
    // keeps both scalers from deriving an odd pixel shape from the even-number rounding of a lane.
    private const string SquarePixels = "setsar=1";

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
                $"-filter:v:{i}", $"scale={lane.Width}:{lane.Height},{SquarePixels}",
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

        AddSegmentsAudioAndOutput(args, manifestPath, hasAudio);

        return string.Join(" ", args);
    }

    /// <summary>
    /// The VAAPI command. Frames reach the GPU through <c>hwupload</c> whether the GPU or the CPU
    /// decoded them, and <c>format=nv12</c> brings 10-bit sources down to the 8 bits H.264
    /// encoders take.
    /// </summary>
    /// <param name="gpuDecode">
    /// Decode on the GPU as well. ffmpeg fails instead of decoding in software when the GPU cannot
    /// decode the source codec (AV1 on older chips), so the caller retries without it.
    /// </param>
    /// <param name="rateControl">See <see cref="GpuRateControl"/>.</param>
    public static string ForGpu(
        string inputPath,
        string manifestPath,
        IReadOnlyList<VideoQualityLane> lanes,
        bool hasAudio,
        string device,
        bool gpuDecode,
        GpuRateControl rateControl)
    {
        var args = new List<string>
        {
            FfmpegThreads.Input,
            "-init_hw_device", Quote($"vaapi=va:{device}"),
            "-filter_hw_device", "va",
        };

        if (gpuDecode)
            args.AddRange(["-hwaccel", "vaapi", "-hwaccel_device", "va"]);

        args.AddRange(["-i", Quote(inputPath), "-filter_complex", Quote(GpuFilterGraph(lanes))]);

        // In both modes -b:v is the lane ceiling, which the DASH muxer writes as its bandwidth.
        // Under constant QP the encoder ignores it.
        for (var i = 0; i < lanes.Count; i++)
        {
            var lane = lanes[i];

            args.AddRange(["-map", $"[v{i}]", $"-c:v:{i}", "h264_vaapi"]);

            if (rateControl == GpuRateControl.ConstantQuality)
            {
                args.AddRange([$"-rc_mode:v:{i}", "CQP", $"-qp:v:{i}", Number(lane.Crf)]);
            }
            else
            {
                args.AddRange([
                    $"-rc_mode:v:{i}", "VBR",
                    $"-maxrate:v:{i}", Kbps(lane.BitrateKbps),
                    $"-bufsize:v:{i}", Kbps(lane.BitrateKbps * BufferSeconds),
                ]);
            }

            args.AddRange([$"-b:v:{i}", Kbps(lane.BitrateKbps)]);
        }

        AddSegmentsAudioAndOutput(args, manifestPath, hasAudio);

        return string.Join(" ", args);
    }

    private static string GpuFilterGraph(IReadOnlyList<VideoQualityLane> lanes)
    {
        var splits = string.Concat(Enumerable.Range(0, lanes.Count).Select(i => $"[s{i}]"));
        var scales = lanes.Select((lane, i) =>
            $"[s{i}]scale_vaapi=w={Number(lane.Width)}:h={Number(lane.Height)}:extra_hw_frames={Number(ExtraGpuFrames)}," +
            $"{SquarePixels}[v{i}]");

        return $"[0:V:0]format=nv12,hwupload,split={Number(lanes.Count)}{splits};{string.Join(";", scales)}";
    }

    private static void AddSegmentsAudioAndOutput(List<string> args, string manifestPath, bool hasAudio)
    {
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
            "-adaptation_sets", Quote(adaptationSets),
            FfmpegThreads.Output,
            Quote(manifestPath),
        ]);
    }

    private static string Quote(string value) => $"\"{value}\"";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Kbps(int value) => $"{Number(value)}k";
}
