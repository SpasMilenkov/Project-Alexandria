namespace Alexandria.Dto.Files;

/// <summary>
/// Content the text sweep still has to read. Storage streams by version, so the newest live
/// version holding the content travels with it, along with the type that picks the reader.
/// </summary>
public sealed record DocumentTextCandidate(Guid ContentObjectId, Guid VersionId, string MimeType);