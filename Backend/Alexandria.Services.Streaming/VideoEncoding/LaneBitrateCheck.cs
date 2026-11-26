using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Alexandria.Services.Streaming.VideoEncoding;

/// <summary>
/// Measures a finished DASH pass against the ceilings its manifest advertises. A segment may
/// carry what the lane's VBV buffer allows: the ceiling over the segment plus the buffer, which
/// for a 6 s segment and a 2 s buffer is a third above the ceiling.
/// </summary>
internal static partial class LaneBitrateCheck
{
    private static readonly XNamespace Mpd = "urn:mpeg:dash:schema:mpd:2011";

    /// <summary>
    /// Returns why the pass does not fit its ceilings, or null when every segment of every lane
    /// fits. A lane that cannot be measured counts as not fitting, because the capped re-encode is
    /// the safe answer.
    /// </summary>
    public static string? FindProblem(string manifestPath, IReadOnlyList<VideoQualityLane> lanes)
    {
        var directory = Path.GetDirectoryName(manifestPath) ?? ".";
        XDocument manifest;

        try
        {
            manifest = XDocument.Load(manifestPath);
        }
        catch (XmlException ex)
        {
            return $"the manifest could not be read: {ex.Message}";
        }

        var representations = manifest
            .Descendants(Mpd + "Representation")
            .Where(representation => representation.Attribute("id") is not null)
            .ToDictionary(representation => representation.Attribute("id")!.Value, StringComparer.Ordinal);

        for (var i = 0; i < lanes.Count; i++)
        {
            var id = i.ToString(CultureInfo.InvariantCulture);

            if (!representations.TryGetValue(id, out var representation))
                return $"lane {id} is missing from the manifest";

            if (FindLaneProblem(directory, id, representation, lanes[i]) is { } problem)
                return problem;
        }

        return null;
    }

    private static string? FindLaneProblem(string directory, string id, XElement representation, VideoQualityLane lane)
    {
        var template = representation.Element(Mpd + "SegmentTemplate") ?? representation.Parent?.Element(Mpd + "SegmentTemplate");
        var timeline = template?.Element(Mpd + "SegmentTimeline")?.Elements(Mpd + "S").ToList();
        var media = template?.Attribute("media")?.Value;

        if (template is null || timeline is null || timeline.Count == 0 || media is null)
            return $"lane {id} has no segment timeline";

        var timescale = LongAttribute(template, "timescale", 1);
        var number = LongAttribute(template, "startNumber", 1);

        foreach (var entry in timeline)
        {
            var seconds = (double)LongAttribute(entry, "d", 0) / timescale;
            var repeats = LongAttribute(entry, "r", 0);

            for (var k = 0; k <= repeats; k++, number++)
            {
                var path = Path.Combine(directory, SegmentName(media, id, number));

                if (!File.Exists(path))
                    return $"segment {Path.GetFileName(path)} of lane {id} is missing";

                var bits = new FileInfo(path).Length * 8.0;
                var allowedBits = lane.BitrateKbps * 1000.0 * (seconds + DashVideoArguments.BufferSeconds);

                if (bits > allowedBits)
                {
                    return $"lane {id} ({lane.Width}x{lane.Height}) peaked at {bits / seconds / 1000:0} kbps " +
                           $"in {Path.GetFileName(path)}, above the {allowedBits / seconds / 1000:0} kbps its " +
                           $"{lane.BitrateKbps} kbps ceiling allows";
                }
            }
        }

        return null;
    }

    private static string SegmentName(string media, string id, long number)
    {
        var named = media.Replace("$RepresentationID$", id, StringComparison.Ordinal);

        return NumberToken().Replace(named, match => match.Groups["width"].Success
            ? number.ToString($"D{match.Groups["width"].Value}", CultureInfo.InvariantCulture)
            : number.ToString(CultureInfo.InvariantCulture));
    }

    private static long LongAttribute(XElement element, string name, long fallback) =>
        long.TryParse(element.Attribute(name)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    [GeneratedRegex(@"\$Number(?:%0(?<width>\d+)d)?\$")]
    private static partial Regex NumberToken();
}
