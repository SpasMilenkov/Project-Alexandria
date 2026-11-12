namespace Alexandria.Dto.Files.Streaming.Lyrics;

/// <summary>The file a transcode was made from, as far as an audit row about it needs.</summary>
public sealed record TrackFileRef(Guid VersionId, Guid FileId, string FileName);
