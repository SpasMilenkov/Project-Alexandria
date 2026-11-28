using Alexandria.Common.Policies;

namespace Alexandria.Common.Helpers;

/// <summary>
/// File-type gate for Transcode rules. An "any file" rule has no type filter of its own, so
/// without this every document and image in scope would be sent to the encoder. The set is the
/// Videos and Audio groups, the same two the rule editor offers for Transcode.
/// </summary>
public static class TranscodeSupportedFileTypes
{
    private const string Groups = FileGroups.Videos + ";" + FileGroups.Audio;

    /// <summary>True when the MIME type canonicalizes to a video or audio type.</summary>
    public static bool IsSupported(string? mimeType)
        => !string.IsNullOrWhiteSpace(mimeType) && FileGroups.MatchesMimeType(Groups, mimeType);
}
