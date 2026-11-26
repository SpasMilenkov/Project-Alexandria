using System.Globalization;

namespace Alexandria.Services.Streaming;

/// <summary>
/// The source's running time as ffprobe reports it, which turns ffmpeg's position into a
/// percentage.
/// </summary>
internal static class MediaDuration
{
    public static string ProbeArguments(string inputPath) =>
        $"-v error -show_entries format=duration -of csv=p=0 \"{inputPath}\"";

    /// <summary>
    /// Returns null for anything that is not a positive number of seconds, such as the
    /// <c>N/A</c> of a stream with no known length.
    /// </summary>
    public static TimeSpan? Parse(string? seconds)
    {
        if (!double.TryParse(seconds?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || !double.IsFinite(value)
            || value <= 0)
            return null;

        return TimeSpan.FromSeconds(value);
    }
}
