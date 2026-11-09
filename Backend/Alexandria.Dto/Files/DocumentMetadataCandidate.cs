namespace Alexandria.Dto.Files;

/// <summary>
/// A document version the backfill still has to read. The type picks the reader, so it travels
/// with the id instead of costing a lookup per version.
/// </summary>
public sealed record DocumentMetadataCandidate(Guid VersionId, string MimeType);
