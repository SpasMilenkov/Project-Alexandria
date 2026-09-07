namespace Alexandria.Common;

/// <summary>
/// Maps ffmpeg DASH segment file names back to zero-based lane indices.
/// Lanes are emitted in rung order, so lane <c>i</c> owns <c>chunk-stream{i}-*</c>
/// and <c>init-stream{i}.*</c>. The single audio stream of video jobs carries the
/// next free index and never matches a representation; shared files such as the
/// manifest match nothing and are ignored.
/// </summary>
public static class DashSegmentClassifier
{
    public static bool TryGetLaneIndex(string objectKey, out int laneIndex)
    {
        laneIndex = -1;

        var fileName = objectKey.AsSpan();
        var slash = fileName.LastIndexOf('/');
        if (slash >= 0)
            fileName = fileName[(slash + 1)..];

        if (!fileName.StartsWith("chunk-stream".AsSpan(), StringComparison.OrdinalIgnoreCase) &&
            !fileName.StartsWith("init-stream".AsSpan(), StringComparison.OrdinalIgnoreCase))
            return false;

        var afterStream = fileName[(fileName.IndexOf(
            "stream".AsSpan(), StringComparison.OrdinalIgnoreCase) + "stream".Length)..];
        var digits = 0;
        while (digits < afterStream.Length && char.IsAsciiDigit(afterStream[digits]))
            digits++;
        if (digits == 0)
            return false;

        if (!int.TryParse(afterStream[..digits], out laneIndex))
        {
            laneIndex = -1;
            return false;
        }

        return true;
    }
}