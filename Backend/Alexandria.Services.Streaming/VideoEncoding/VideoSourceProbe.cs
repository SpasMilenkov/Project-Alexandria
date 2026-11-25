using System.Globalization;
using System.Text.Json;

namespace Alexandria.Services.Streaming.VideoEncoding;

/// <summary>
/// Reads the source's shape and whether it has audio with one ffprobe call, before any lane is
/// sized from it.
/// </summary>
internal static class VideoSourceProbe
{
    public static string Arguments(string inputPath) =>
        "-v error -show_entries stream=codec_type,width,height,sample_aspect_ratio" +
        ":stream_disposition=attached_pic:stream_side_data=rotation -of json " +
        $"\"{inputPath}\"";

    /// <summary>
    /// Returns null when the source has no video stream to encode.
    /// </summary>
    public static VideoSource? Parse(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("streams", out var streamList))
            return null;

        var streams = streamList.EnumerateArray().ToList();
        var hasAudio = streams.Any(stream => CodecType(stream) == "audio");

        // Matches the encoder's `0:V:0`: embedded cover art is a video stream too, but not the video.
        var video = streams.FirstOrDefault(stream => CodecType(stream) == "video" && !IsAttachedPicture(stream));

        if (video.ValueKind != JsonValueKind.Object
            || !video.TryGetProperty("width", out var widthValue)
            || !video.TryGetProperty("height", out var heightValue))
            return null;

        // Anamorphic sources store fewer columns than they show; players stretch them back by
        // the sample aspect ratio, so the shown width is what a lane has to match.
        var width = (int)Math.Round(widthValue.GetInt32() * SampleAspectRatio(video));
        var height = heightValue.GetInt32();

        // ffmpeg turns frames upright before scaling them, so a phone clip stored sideways with
        // a rotation tag reaches the scaler with its sides swapped.
        if (IsQuarterTurn(video))
            (width, height) = (height, width);

        return new VideoSource(width, height, hasAudio);
    }

    private static string? CodecType(JsonElement stream) =>
        stream.TryGetProperty("codec_type", out var type) ? type.GetString() : null;

    private static bool IsAttachedPicture(JsonElement stream) =>
        stream.TryGetProperty("disposition", out var disposition)
        && disposition.TryGetProperty("attached_pic", out var attached)
        && attached.GetInt32() == 1;

    // ffprobe writes 0:1 when the container does not say, which means square pixels.
    private static double SampleAspectRatio(JsonElement stream)
    {
        if (!stream.TryGetProperty("sample_aspect_ratio", out var value))
            return 1;

        var parts = (value.GetString() ?? "").Split(':');

        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var numerator)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var denominator)
            || numerator == 0
            || denominator == 0)
            return 1;

        return (double)numerator / denominator;
    }

    private static bool IsQuarterTurn(JsonElement stream)
    {
        if (!stream.TryGetProperty("side_data_list", out var sideData))
            return false;

        foreach (var entry in sideData.EnumerateArray())
        {
            if (!entry.TryGetProperty("rotation", out var rotation))
                continue;

            var degrees = ((int)Math.Round(rotation.GetDouble()) % 360 + 360) % 360;

            return degrees is 90 or 270;
        }

        return false;
    }
}
